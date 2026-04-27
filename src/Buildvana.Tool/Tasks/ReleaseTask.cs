// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Linq;
using System.Threading.Tasks;
using Buildvana.Tool.Infrastructure;
using Buildvana.Tool.Services;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.PublicApiFiles;
using Buildvana.Tool.Services.ServerAdapters;
using Buildvana.Tool.Services.Versioning;
using Buildvana.Tool.Utilities;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Frosting;
using CommunityToolkit.Diagnostics;

using SysFile = System.IO.File;
using SysPath = System.IO.Path;

namespace Buildvana.Tool.Tasks;

[TaskName(Name)]
[TaskDescription(Description)]
[IsDependentOn(typeof(TestTask))]
public sealed class ReleaseTask : AsyncFrostingTask<BuildContext>
{
    private const string Name = "Release";
    private const string Description = "Publish a new public release (CI only)";

    public override async Task RunAsync(BuildContext context)
    {
        Guard.IsNotNull(context);

        var options = context.GetService<OptionsService>();
        var server = context.GetService<ServerAdapter>();
        var version = context.GetService<VersionService>();
        var dotnet = context.GetService<DotNetService>();
        var git = context.GetService<GitService>();
        var changelog = context.GetService<ChangelogService>();
        var publicApiFiles = context.GetService<PublicApiFilesService>();
        var docfx = context.GetService<DocFxService>();
        var selfReferenceUpdater = context.GetService<SelfReferenceUpdater>();

        // Perform some preliminary checks
        context.Ensure(server.IsCloudBuild, "A release can only be created on a known cloud build platform.");
        context.Ensure(!string.IsNullOrEmpty(git.CurrentBranch), "A release can only be created from a branch.");
        context.Ensure(version.IsPublicRelease, "Cannot create a release from the current branch.");

        // Ensure that the CI bot identity is used for commits, if not already set.
        git.CommitterIdentity ??= server.CIBotIdentity ?? context.Fail<GitIdentity>("Cannot determine a committer identity for release commits. Configure git config user.name/user.email before running this task.");
        context.Information($"Using committer identity: {git.CommitterIdentity.Name} <{git.CommitterIdentity.Email}>");

        // Set fallback Git credentials if the server adapter can provide them.
        var pushUsername = server.PushUsername;
        var pushPassword = server.PushPassword;
        if (pushUsername is not null && pushPassword is not null)
        {
            context.Information($"Fallback push credentials provided by the server adapter (protocol username: '{pushUsername}').");
            git.PushCredentialsFallback = new(pushUsername, pushPassword);
        }
        else
        {
            context.Warning("No push credentials provided by the server adapter. Push operations may fail if the repository is not already authenticated.");
        }

        // Perform an initial versioning consistency check.
        // This is a tad more relaxed than the final check, as it takes into account that we may still increment the current version
        // (for example by updating the changelog).
        version.EnsureConsistency(false);

        // Compute the version spec change to apply, if any.
        // This implies more checks and possibly throws, so do it as early as possible.
        var versionSpecChange = version.ComputeVersionSpecChange(
            requestedChange: options.GetOption("versionSpecChange", VersionSpecChange.None),
            checkPublicApiFiles: options.GetOption("checkPublicApiFiles", true));

        var release = await server.CreateReleaseAsync().ConfigureAwait(false);
        await using (release.ConfigureAwait(false))
        {
            // Modify version file if required.
            if (versionSpecChange != VersionSpecChange.None)
            {
                var versionFile = VersionFile.Load(context);
                var previousVersionSpec = versionFile.VersionSpec;
                if (versionFile.ApplyVersionSpecChange(versionSpecChange))
                {
                    context.Information($"Version spec changed from {previousVersionSpec} to {versionFile.VersionSpec}.");
                    versionFile.Save();
                    release.UpdateRepository(versionFile.Path);
                }
                else
                {
                    context.Information("Version spec not changed.");
                }
            }

            // Update public API files only when releasing a stable version
            if (version.IsPrerelease)
            {
                context.Information("Public API update skipped: not needed on prerelease.");
            }
            else
            {
                var modified = publicApiFiles.TransferAllPublicApisToShipped().ToArray();
                context.Information(modified.Length switch {
                    0 => "No public API files were modified.",
                    1 => "1 public API file was modified.",
                    _ => $"{modified.Length} public API files were modified.",
                });

                if (modified.Length > 0)
                {
                    release.UpdateRepository(modified);
                }
            }

            // Update changelog only on non-prerelease, unless forced
            var changelogUpdated = false;
            if (!changelog.Exists)
            {
                context.Information($"Changelog update skipped: {changelog.Path} not found.");
            }
            else if (!version.IsPrerelease || options.GetOption("updateChangelogOnPrerelease", false))
            {
                if (options.GetOption("ensureChangelogNotEmpty", true))
                {
                    context.Ensure(
                        changelog.HasUnreleasedChanges(),
                        "Changelog check failed: the \"Unreleased changes\" section is empty or only contains sub-section headings.");

                    context.Information("Changelog check successful: the \"Unreleased changes\" section is not empty.");
                }
                else
                {
                    context.Information("Changelog check skipped: option 'ensureChangelogNotEmpty' is false.");
                }

                // Update the changelog and commit the change before building.
                // This ensures that the Git height is up to date when computing a version for the build artifacts.
                changelog.PrepareForRelease();
                release.UpdateRepository(changelog.Path);
                changelogUpdated = true;
            }
            else
            {
                context.Information("Changelog update skipped: not needed on prerelease.");
            }

            // At this point we know what the actual published version will be.
            // Time for a final consistency check.
            version.EnsureConsistency(true);

            // Ensure that the release tag doesn't already exist.
            // This assumes that full repo history has been checked out;
            // however, that is already a prerequisite for using Nerdbank.GitVersioning.
            context.Ensure(!git.TagExists(version.CurrentStr), $"Tag '{version.CurrentStr}' already exists in repository.");

            // Build, test, make artifacts
            dotnet.RestoreSolution();
            dotnet.BuildSolution(false);
            dotnet.TestSolution(false, false, false);
            dotnet.PackSolution(false, false);

            if (changelogUpdated)
            {
                // Change the new section's title in the changelog to reflect the actual version.
                changelog.UpdateNewSectionTitle();
                release.UpdateRepository(changelog.Path);
            }
            else
            {
                context.Information("Changelog section title update skipped: changelog has not been updated.");
            }

            // Update in-tree references to packages produced by this release (dogfooding).
            // Must happen after pack (so the produced .nupkg files exist and the build ran against the
            // previously-published versions) and before push (so the rewrites land in the Prepare release commit).
            if (options.GetOption("updateSelfReferences", true))
            {
                var selfReferenceUpdates = selfReferenceUpdater.UpdateReferences();
                context.Information(selfReferenceUpdates.Count switch {
                    0 => "No self-referenced files were modified.",
                    1 => "1 self-referenced file was modified.",
                    _ => $"{selfReferenceUpdates.Count} self-referenced files were modified.",
                });

                if (selfReferenceUpdates.Count > 0)
                {
                    release.UpdateRepository([..selfReferenceUpdates]);
                }
            }
            else
            {
                context.Information("Self-reference update skipped: option 'updateSelfReferences' is false.");
            }

            release.PushUpdates();

            // Publish NuGet packages
            await dotnet.NuGetPushAllAsync().ConfigureAwait(false);

            // Gather build assets from Buildvana.Sdk release asset lists
            const string assetListMask = "*.assets.txt";
            context.Information("Reading release asset lists...");
            var assetLists = SysPath.Combine(dotnet.ArtifactsPath.FullPath, assetListMask);
            foreach (var path in context.GetFiles(assetLists).Select(x => x.FullPath))
            {
                context.Verbose("Reading release asset list {path}...");
                var i = 0;
                await foreach (var line in SysFile.ReadLinesAsync(path).ConfigureAwait(false))
                {
                    i++;
                    var parts = line.Split('\t');
                    if (parts.Length != 3)
                    {
                        context.Warning($"Release asset list {path}, line #{i}: invalid line '{line}'");
                        continue;
                    }

                    if (!SysFile.Exists(parts[0]))
                    {
                        context.Warning($"Release asset list {path}, line #{i}: asset not found '{parts[0]}'");
                        continue;
                    }

                    release.AddAsset(path: parts[0], description: parts[2], mimeType: parts[1]);
                }
            }

            // Add NuGet packages as assets
            // const string nupkgMask = "*.nupkg";
            // var nupkgs = SysPath.Combine(dotnet.ArtifactsPath.FullPath, nupkgMask);
            foreach (var path in context.GetFiles(assetLists).Select(x => x.FullPath))
            {
                release.AddAsset(path);
                var snupkgPath = SysPath.ChangeExtension(path, ".snupkg");
                if (SysFile.Exists(snupkgPath))
                {
                    release.AddAsset(snupkgPath);
                }
            }

            // Generate documentation
            if (docfx.IsEnabled)
            {
                if (version.IsPrerelease)
                {
                    context.Information("Documentation generation skipped: not needed on prerelease.");
                }
                else if (git.CurrentBranch != git.MainBranch)
                {
                    context.Information($"Documentation generation skipped: releasing from '{git.CurrentBranch}', not '{git.MainBranch}'.");
                }
                else
                {
                    context.Information("Generating documentation web pages...");
                    await docfx.GenerateSiteAsync().ConfigureAwait(false);
                    context.Information("Generating documentation PDF files...");
                    await docfx.GeneratePdfsAsync().ConfigureAwait(false);
                }
            }

            // Last but not least, publish the release.
            await release.PublishAsync().ConfigureAwait(false);
        }
    }
}
