// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Linq;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Json;
using Buildvana.Tool.Infrastructure;
using Buildvana.Tool.Services;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.PublicApiFiles;
using Buildvana.Tool.Services.ServerAdapters;
using Buildvana.Tool.Services.Versioning;
using Cake.Common.IO;
using Cake.Frosting;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;

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

        var logger = context.GetService<ILogger<ReleaseTask>>();
        var home = context.GetService<IHomeDirectoryProvider>();
        var jsonHelper = context.GetService<IJsonHelper>();
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
        BuildFailedException.ThrowIfNot(server.IsCloudBuild, "A release can only be created on a known cloud build platform.");
        BuildFailedException.ThrowIfNot(!string.IsNullOrEmpty(git.CurrentBranch), "A release can only be created from a branch.");
        BuildFailedException.ThrowIfNot(version.IsPublicRelease, "Cannot create a release from the current branch.");

        // Ensure that the CI bot identity is used for commits, if not already set.
        git.CommitterIdentity ??= server.CIBotIdentity ?? throw new BuildFailedException("Cannot determine a committer identity for release commits. Configure git config user.name/user.email before running this task.");
        logger.LogInformation("Using committer identity: {Name} <{Email}>", git.CommitterIdentity.Name, git.CommitterIdentity.Email);

        // Set fallback Git credentials if the server adapter can provide them.
        var pushUsername = server.PushUsername;
        var pushPassword = server.PushPassword;
        if (pushUsername is not null && pushPassword is not null)
        {
            logger.LogInformation("Fallback push credentials provided by the server adapter (protocol username: '{Username}').", pushUsername);
            git.PushCredentialsFallback = new(pushUsername, pushPassword);
        }
        else
        {
            logger.LogWarning("No push credentials provided by the server adapter. Push operations may fail if the repository is not already authenticated.");
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
                var versionFile = VersionFile.Load(home, jsonHelper);
                var previousVersionSpec = versionFile.VersionSpec;
                if (versionFile.ApplyVersionSpecChange(versionSpecChange))
                {
                    logger.LogInformation("Version spec changed from {Previous} to {New}.", previousVersionSpec, versionFile.VersionSpec);
                    versionFile.Save();
                    release.UpdateRepository(versionFile.Path);
                }
                else
                {
                    logger.LogInformation("Version spec not changed.");
                }
            }

            // Update public API files only when releasing a stable version
            if (version.IsPrerelease)
            {
                logger.LogInformation("Public API update skipped: not needed on prerelease.");
            }
            else
            {
                var modified = publicApiFiles.TransferAllPublicApisToShipped().ToArray();
                switch (modified.Length)
                {
                    case 0:
                        logger.LogInformation("No public API files were modified.");
                        break;
                    case 1:
                        logger.LogInformation("1 public API file was modified.");
                        break;
                    default:
                        logger.LogInformation("{Count} public API files were modified.", modified.Length);
                        break;
                }

                if (modified.Length > 0)
                {
                    release.UpdateRepository(modified);
                }
            }

            // Update changelog only on non-prerelease, unless forced
            var changelogUpdated = false;
            if (!changelog.Exists)
            {
                logger.LogInformation("Changelog update skipped: {Path} not found.", changelog.Path);
            }
            else if (!version.IsPrerelease || options.GetOption("updateChangelogOnPrerelease", false))
            {
                if (options.GetOption("ensureChangelogNotEmpty", true))
                {
                    BuildFailedException.ThrowIfNot(
                        changelog.HasUnreleasedChanges(),
                        "Changelog check failed: the \"Unreleased changes\" section is empty or only contains sub-section headings.");

                    logger.LogInformation("Changelog check successful: the \"Unreleased changes\" section is not empty.");
                }
                else
                {
                    logger.LogInformation("Changelog check skipped: option 'ensureChangelogNotEmpty' is false.");
                }

                // Update the changelog and commit the change before building.
                // This ensures that the Git height is up to date when computing a version for the build artifacts.
                changelog.PrepareForRelease();
                release.UpdateRepository(changelog.Path);
                changelogUpdated = true;
            }
            else
            {
                logger.LogInformation("Changelog update skipped: not needed on prerelease.");
            }

            // At this point we know what the actual published version will be.
            // Time for a final consistency check.
            version.EnsureConsistency(true);

            // Ensure that the release tag doesn't already exist.
            // This assumes that full repo history has been checked out;
            // however, that is already a prerequisite for using Nerdbank.GitVersioning.
            BuildFailedException.ThrowIfNot(!git.TagExists(version.CurrentStr), $"Tag '{version.CurrentStr}' already exists in repository.");

            // Build, test, make artifacts
            dotnet.RestoreSolution();
            dotnet.BuildSolution(false);
            dotnet.TestSolution(false, false);
            dotnet.PackSolution(false, false);

            if (changelogUpdated)
            {
                // Change the new section's title in the changelog to reflect the actual version.
                changelog.UpdateNewSectionTitle();
                release.UpdateRepository(changelog.Path);
            }
            else
            {
                logger.LogInformation("Changelog section title update skipped: changelog has not been updated.");
            }

            // Update in-tree references to packages produced by this release (dogfooding).
            // Must happen after pack (so the produced .nupkg files exist and the build ran against the
            // previously-published versions) and before push (so the rewrites travel with the release commit).
            // Goes into a separate commit so the tagged "Prepare release" commit reflects the actual built
            // state (which still references the previously-published versions); the dogfood commit is marked
            // [skip ci] because the new packages aren't in the feed yet at push time.
            if (options.GetOption("updateSelfReferences", true))
            {
                var selfReferenceUpdates = selfReferenceUpdater.UpdateReferences();
                switch (selfReferenceUpdates.Count)
                {
                    case 0:
                        logger.LogInformation("No self-referenced files were modified.");
                        break;
                    case 1:
                        logger.LogInformation("1 self-referenced file was modified.");
                        break;
                    default:
                        logger.LogInformation("{Count} self-referenced files were modified.", selfReferenceUpdates.Count);
                        break;
                }

                if (selfReferenceUpdates.Count > 0)
                {
                    release.AddPostReleaseCommit(
                        $"Update self-references to {version.CurrentStr} [skip ci]",
                        [..selfReferenceUpdates]);
                }
            }
            else
            {
                logger.LogInformation("Self-reference update skipped: option 'updateSelfReferences' is false.");
            }

            release.PushUpdates();

            // Publish NuGet packages
            await dotnet.NuGetPushAllAsync().ConfigureAwait(false);

            // Gather build assets from Buildvana.Sdk release asset lists
            const string assetListMask = "*.assets.txt";
            logger.LogInformation("Reading release asset lists...");
            var assetLists = SysPath.Combine(dotnet.ArtifactsPath.FullPath, assetListMask);
            foreach (var path in context.GetFiles(assetLists).Select(x => x.FullPath))
            {
                logger.LogDebug("Reading release asset list {Path}...", path);
                var i = 0;
                await foreach (var line in SysFile.ReadLinesAsync(path).ConfigureAwait(false))
                {
                    i++;
                    var parts = line.Split('\t');
                    if (parts.Length != 3)
                    {
                        logger.LogWarning("Release asset list {Path}, line #{LineNumber}: invalid line '{Line}'", path, i, line);
                        continue;
                    }

                    if (!SysFile.Exists(parts[0]))
                    {
                        logger.LogWarning("Release asset list {Path}, line #{LineNumber}: asset not found '{Asset}'", path, i, parts[0]);
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
                    logger.LogInformation("Documentation generation skipped: not needed on prerelease.");
                }
                else if (git.CurrentBranch != git.MainBranch)
                {
                    logger.LogInformation("Documentation generation skipped: releasing from '{Current}', not '{Main}'.", git.CurrentBranch, git.MainBranch);
                }
                else
                {
                    logger.LogInformation("Generating documentation web pages...");
                    await docfx.GenerateSiteAsync().ConfigureAwait(false);
                    logger.LogInformation("Generating documentation PDF files...");
                    await docfx.GeneratePdfsAsync().ConfigureAwait(false);
                }
            }

            // Last but not least, publish the release.
            await release.PublishAsync().ConfigureAwait(false);
        }
    }
}
