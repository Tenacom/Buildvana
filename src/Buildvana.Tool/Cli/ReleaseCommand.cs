// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Json;
using Buildvana.Tool.Services;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.PublicApiFiles;
using Buildvana.Tool.Services.ServerAdapters;
using Buildvana.Tool.Services.Solution;
using Buildvana.Tool.Services.Versioning;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;

namespace Buildvana.Tool.Cli;

[AcceptsMSBuildOptions(MSBuildOptionKinds.All)]
[Description("Publish a new public release (CI only).")]
internal sealed class ReleaseCommand(IServiceProvider services) : AsyncCommand<ReleaseSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, ReleaseSettings settings, CancellationToken cancellationToken)
    {
        Guard.IsNotNull(settings);
        services.GetRequiredService<BuildSettingsHolder>().Current = settings;

        // Pre-pipeline (mirrors today's [IsDependentOn(TestTask)] chain).
        await BuildSteps.CleanAsync(services).ConfigureAwait(false);
        await BuildSteps.RestoreAsync(services).ConfigureAwait(false);
        await BuildSteps.BuildAsync(services).ConfigureAwait(false);
        await BuildSteps.TestAsync(services).ConfigureAwait(false);

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Release");
        var home = services.GetRequiredService<IHomeDirectoryProvider>();
        var jsonHelper = services.GetRequiredService<IJsonHelper>();
        var server = services.GetRequiredService<ServerAdapter>();
        var version = services.GetRequiredService<VersionService>();
        var dotnet = services.GetRequiredService<DotNetService>();
        var solution = services.GetRequiredService<SolutionContext>();
        var git = services.GetRequiredService<GitService>();
        var changelog = services.GetRequiredService<ChangelogService>();
        var publicApiFiles = services.GetRequiredService<PublicApiFilesService>();
        var docfx = services.GetRequiredService<DocFxService>();
        var selfReferenceUpdater = services.GetRequiredService<SelfReferenceUpdater>();

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
            requestedChange: settings.ResolveBump(),
            checkPublicApiFiles: settings.ResolveCheckPublicApi());

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
                logger.LogInformation("Changelog update skipped: {Path} not found.", ChangelogService.FileName);
            }
            else if (!version.IsPrerelease || settings.ResolveUnstableChangelog())
            {
                if (settings.ResolveRequireChangelog())
                {
                    BuildFailedException.ThrowIfNot(
                        changelog.HasUnreleasedChanges(),
                        "Changelog check failed: the \"Unreleased changes\" section is empty or only contains sub-section headings.");

                    logger.LogInformation("Changelog check successful: the \"Unreleased changes\" section is not empty.");
                }
                else
                {
                    logger.LogInformation("Changelog check skipped: option 'requireChangelog' is false.");
                }

                // Update the changelog and commit the change before building.
                // This ensures that the Git height is up to date when computing a version for the build artifacts.
                changelog.PrepareForRelease();
                release.UpdateRepository(ChangelogService.FileName);
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
            await dotnet.RestoreSolutionAsync(solution).ConfigureAwait(false);
            await dotnet.BuildSolutionAsync(solution, false).ConfigureAwait(false);
            await dotnet.TestSolutionAsync(solution, false, false).ConfigureAwait(false);
            await dotnet.PackSolutionAsync(solution, false, false).ConfigureAwait(false);

            if (changelogUpdated)
            {
                // Change the new section's title in the changelog to reflect the actual version.
                changelog.UpdateNewSectionTitle();
                release.UpdateRepository(ChangelogService.FileName);
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
            if (settings.ResolveDogfood())
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
                logger.LogInformation("Self-reference update skipped: option 'dogfood' is false.");
            }

            release.PushUpdates();

            // Publish NuGet packages
            await dotnet.NuGetPushAllAsync().ConfigureAwait(false);

            // Gather build assets from Buildvana.Sdk release asset lists
            logger.LogInformation("Reading release asset lists...");
            foreach (var path in FileSystemHelper.EnumerateFiles(dotnet.ArtifactsPath, "*.assets.txt"))
            {
                logger.LogDebug("Reading release asset list {Path}...", path);
                var i = 0;
                await foreach (var line in File.ReadLinesAsync(path, cancellationToken).ConfigureAwait(false))
                {
                    i++;
                    var parts = line.Split('\t');
                    if (parts.Length != 3)
                    {
                        logger.LogWarning("Release asset list {Path}, line #{LineNumber}: invalid line '{Line}'", path, i, line);
                        continue;
                    }

                    if (!File.Exists(parts[0]))
                    {
                        logger.LogWarning("Release asset list {Path}, line #{LineNumber}: asset not found '{Asset}'", path, i, parts[0]);
                        continue;
                    }

                    release.AddAsset(path: parts[0], description: parts[2], mimeType: parts[1]);
                }
            }

            // Add NuGet packages as assets
            foreach (var path in FileSystemHelper.EnumerateFiles(dotnet.ArtifactsPath, "*.nupkg"))
            {
                release.AddAsset(path);
                var snupkgPath = Path.ChangeExtension(path, ".snupkg");
                if (File.Exists(snupkgPath))
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

        return 0;
    }
}
