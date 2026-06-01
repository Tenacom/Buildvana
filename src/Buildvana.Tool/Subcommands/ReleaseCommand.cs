// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.Configuration;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Json;
using Buildvana.Tool.Build;
using Buildvana.Tool.Infrastructure;
using Buildvana.Tool.Infrastructure.Execution;
using Buildvana.Tool.Services;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.PublicApiFiles;
using Buildvana.Tool.Services.ServerAdapters;
using Buildvana.Tool.Services.Versioning;
using Buildvana.Tool.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Buildvana.Tool.Subcommands;

[ImplementsCommand("release", settingsType: typeof(ReleaseSettings))]
[Description("Publish a new public release (CI only).")]
internal sealed class ReleaseCommand(IServiceProvider services, ReleaseSettings settings, BuildPipeline pipeline) : IBvCommand
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        var reporter = services.GetRequiredService<IReporter>();
        using var activity = reporter.BeginActivity("Release");

        var configuration = settings.ResolveConfiguration();
        var artifactsPath = Path.Combine(CommonPaths.AllArtifacts, configuration);

        // Verification pass (Clean→Test), mirroring today's [IsDependentOn(TestTask)] chain.
        await pipeline.RunThroughAsync(BuildStep.Test, configuration, cancellationToken).ConfigureAwait(false);

        var home = services.GetRequiredService<IHomeDirectoryProvider>();
        var jsonHelper = services.GetRequiredService<IJsonHelper>();
        var server = services.GetRequiredService<ServerAdapter>();
        var version = services.GetRequiredService<VersionService>();
        var dotnet = services.GetRequiredService<DotNetService>();
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
        reporter.Info($"Using committer identity: {git.CommitterIdentity.Name} <{git.CommitterIdentity.Email}>");

        // Set fallback Git credentials if the server adapter can provide them.
        var pushUsername = server.PushUsername;
        var pushPassword = server.PushPassword;
        if (pushUsername is not null && pushPassword is not null)
        {
            reporter.Info($"Fallback push credentials provided by the server adapter (protocol username: '{pushUsername}').");
            git.PushCredentialsFallback = new(pushUsername, pushPassword);
        }
        else
        {
            reporter.Warning("No push credentials provided by the server adapter. Push operations may fail if the repository is not already authenticated.");
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
                    reporter.Info($"Version spec changed from {previousVersionSpec} to {versionFile.VersionSpec}.");
                    versionFile.Save();
                    release.UpdateRepository(versionFile.Path);
                }
                else
                {
                    reporter.Info("Version spec not changed.");
                }
            }

            // Update public API files only when releasing a stable version
            if (version.IsPrerelease)
            {
                reporter.Info("Public API update skipped: not needed on prerelease.");
            }
            else
            {
                var modified = publicApiFiles.TransferAllPublicApisToShipped().ToArray();
                switch (modified.Length)
                {
                    case 0:
                        reporter.Info("No public API files were modified.");
                        break;
                    case 1:
                        reporter.Info("1 public API file was modified.");
                        break;
                    default:
                        reporter.Info(string.Create(CultureInfo.InvariantCulture, $"{modified.Length} public API files were modified."));
                        break;
                }

                if (modified.Length > 0)
                {
                    release.UpdateRepository(modified);
                }
            }

            // Update changelog according to the configured policy (none | stable | all).
            var changelogUpdated = false;
            var changelogUpdates = settings.ResolveChangelogUpdates();
            var shouldUpdateChangelog = changelogUpdates != ChangelogUpdates.None
                && (changelogUpdates == ChangelogUpdates.All || !version.IsPrerelease);
            if (!changelog.Exists)
            {
                reporter.Info($"Changelog update skipped: {ChangelogService.FileName} not found.");
            }
            else if (!shouldUpdateChangelog)
            {
                var reason = changelogUpdates == ChangelogUpdates.None
                    ? "changelog updates are disabled (release.changelogUpdates is 'none')."
                    : "not needed on prerelease.";
                reporter.Info($"Changelog update skipped: {reason}");
            }
            else
            {
                // An empty "Unreleased changes" section is substituted from release.emptyChangelog when configured;
                // otherwise the release fails.
                string? emptyChangelogSubstitute = null;
                if (changelog.HasUnreleasedChanges())
                {
                    reporter.Info("Changelog check successful: the \"Unreleased changes\" section is not empty.");
                }
                else
                {
                    emptyChangelogSubstitute = settings.ResolveEmptyChangelog();
                    BuildFailedException.ThrowIfNot(
                        emptyChangelogSubstitute is not null,
                        "Changelog check failed: the \"Unreleased changes\" section is empty or only contains sub-section headings, and no substitute text is configured (release.emptyChangelog).");

                    reporter.Info("Changelog \"Unreleased changes\" section is empty; substituting the configured release.emptyChangelog text.");
                }

                // Update the changelog and commit the change before building.
                // This ensures that the Git height is up to date when computing a version for the build artifacts.
                changelog.PrepareForRelease(emptyChangelogSubstitute);
                release.UpdateRepository(ChangelogService.FileName);
                changelogUpdated = true;
            }

            // At this point we know what the actual published version will be.
            // Time for a final consistency check.
            version.EnsureConsistency(true);

            // Ensure that the release tag doesn't already exist.
            // This assumes that full repo history has been checked out;
            // however, that is already a prerequisite for using Nerdbank.GitVersioning.
            BuildFailedException.ThrowIfNot(!git.TagExists(version.CurrentStr), $"Tag '{version.CurrentStr}' already exists in repository.");

            // Artifact pass (Restore→Pack, no Clean): rebuild against the resolved version and make artifacts.
            await pipeline.RunRangeAsync(BuildStep.Restore, BuildStep.Pack, configuration, cancellationToken).ConfigureAwait(false);

            if (changelogUpdated)
            {
                // Change the new section's title in the changelog to reflect the actual version.
                changelog.UpdateNewSectionTitle();
                release.UpdateRepository(ChangelogService.FileName);
            }
            else
            {
                reporter.Info("Changelog section title update skipped: changelog has not been updated.");
            }

            // Update in-tree references to packages produced by this release (dogfooding).
            // Must happen after pack (so the produced .nupkg files exist and the build ran against the
            // previously-published versions) and before push (so the rewrites travel with the release commit).
            // Goes into a separate commit so the tagged "Prepare release" commit reflects the actual built
            // state (which still references the previously-published versions); the dogfood commit is marked
            // [skip ci] because the new packages aren't in the feed yet at push time.
            if (settings.ResolveDogfood())
            {
                var selfReferenceUpdates = selfReferenceUpdater.UpdateReferences(artifactsPath);
                switch (selfReferenceUpdates.Count)
                {
                    case 0:
                        reporter.Info("No self-referenced files were modified.");
                        break;
                    case 1:
                        reporter.Info("1 self-referenced file was modified.");
                        break;
                    default:
                        reporter.Info(string.Create(CultureInfo.InvariantCulture, $"{selfReferenceUpdates.Count} self-referenced files were modified."));
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
                reporter.Info("Self-reference update skipped: option 'dogfood' is false.");
            }

            release.PushUpdates();

            // Publish NuGet packages
            await dotnet.NuGetPushAllAsync(artifactsPath, cancellationToken).ConfigureAwait(false);

            // Gather build assets from Buildvana.Sdk release asset lists
            reporter.Info("Reading release asset lists...");
            foreach (var path in FileSystemHelper.EnumerateFiles(artifactsPath, "*.assets.txt"))
            {
                reporter.Detail($"Reading release asset list {path}...");
                var i = 0;
                await foreach (var line in File.ReadLinesAsync(path, cancellationToken).ConfigureAwait(false))
                {
                    i++;
                    var parts = line.Split('\t');
                    if (parts.Length != 3)
                    {
                        reporter.Warning(string.Create(CultureInfo.InvariantCulture, $"Release asset list {path}, line #{i}: invalid line '{line}'"));
                        continue;
                    }

                    if (!File.Exists(parts[0]))
                    {
                        reporter.Warning(string.Create(CultureInfo.InvariantCulture, $"Release asset list {path}, line #{i}: asset not found '{parts[0]}'"));
                        continue;
                    }

                    release.AddAsset(path: parts[0], description: parts[2], mimeType: parts[1]);
                }
            }

            // Add NuGet packages as assets
            foreach (var path in FileSystemHelper.EnumerateFiles(artifactsPath, "*.nupkg"))
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
                    reporter.Info("Documentation generation skipped: not needed on prerelease.");
                }
                else if (!settings.MatchesDocsBranch(git.CurrentBranch))
                {
                    reporter.Info($"Documentation generation skipped: branch '{git.CurrentBranch}' does not match release.generateDocsFrom.");
                }
                else
                {
                    reporter.Info("Generating documentation web pages...");
                    await docfx.GenerateSiteAsync().ConfigureAwait(false);
                    reporter.Info("Generating documentation PDF files...");
                    await docfx.GeneratePdfsAsync().ConfigureAwait(false);
                }
            }

            // Last but not least, publish the release.
            await release.PublishAsync().ConfigureAwait(false);
        }

        activity.Complete();
        return 0;
    }
}
