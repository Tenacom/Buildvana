// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Versioning;
using Buildvana.Runtime;
using Buildvana.Tool.Build;
using Buildvana.Tool.Infrastructure;
using Buildvana.Tool.Infrastructure.Execution;
using Buildvana.Tool.Services;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.Hooks;
using Buildvana.Tool.Services.PublicApiFiles;
using Buildvana.Tool.Services.ServerAdapters;
using Buildvana.Tool.Services.Versioning;
using Buildvana.Tool.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Buildvana.Tool.Subcommands;

[ImplementsCommand("release", settingsType: typeof(ReleaseSettings), usesSdk: true)]
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
        var versioningSettings = services.GetRequiredService<VersioningSettings>();
        var server = services.GetRequiredService<ServerAdapter>();
        var version = services.GetRequiredService<VersionService>();
        var dotnet = services.GetRequiredService<DotNetService>();
        var git = services.GetRequiredService<GitService>();
        var changelog = services.GetRequiredService<ChangelogService>();
        var publicApiFiles = services.GetRequiredService<PublicApiFilesService>();
        var docfx = services.GetRequiredService<DocFxService>();
        var selfReferenceUpdater = services.GetRequiredService<SelfReferenceUpdater>();
        var hookRunner = services.GetRequiredService<HookRunner>();

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
                var versionFile = VersionFile.Load(home);
                var previousVersionSpec = versionFile.Spec;
                if (versionFile.ApplyChange(versionSpecChange))
                {
                    reporter.Info($"Version spec changed from {previousVersionSpec} to {versionFile.Spec}.");
                    versionFile.Save(versioningSettings.PrereleaseTag);
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

            // Create the release commit now, if none of the steps above had anything to commit.
            // The Git height, hence the version, must be final before anything is built: a release commit
            // created later would bump the height after the fact, tagging and publishing a version one
            // patch above the one the artifacts were built with. This call is also what makes the later
            // AddPostReleaseCommit legal, as that method requires the release commit to exist already.
            release.EnsureReleaseCommit();

            // At this point we know what the actual published version will be.
            // Time for a final consistency check.
            version.EnsureConsistency(true);

            // Ensure that the release tag doesn't already exist.
            // This assumes that full repo history has been checked out;
            // however, that is already a prerequisite for computing the Git height.
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

            // Discover the packages produced by the pack step; both the post-release hook context and
            // the self-reference update consume the map.
            var producedPackages = ArtifactsHelper.DiscoverProducedPackages(artifactsPath, version.CurrentStr, reporter);
            var dogfooded = settings.ResolveDogfood();

            // Run the repo-owned post-release hook, if present. It runs whether or not dogfooding is
            // enabled; the files it changes are detected by snapshotting the working tree around it
            // and join the post-release commit alongside the self-reference rewrites.
            // The hook must run before the self-reference update: it is a file-based app inside the
            // repository tree, so building it resolves the in-tree version pins (e.g. the Buildvana.Sdk
            // entry in global.json), and the packages produced by this release reach a feed only after
            // publish. A hook that needs the new versions can read them from the context's
            // Release properties instead of the rewritten files.
            var hookContext = new PostReleaseHookContext
            {
                Paths = new()
                {
                    HomeDirectory = home.HomeDirectory,
                    ArtifactsDirectory = Path.GetFullPath(artifactsPath),
                    ScratchDirectory = Path.GetFullPath(CommonPaths.Scratch, home.HomeDirectory),
                },
                Release = new()
                {
                    Version = version.CurrentSimpleStr,
                    SemVer = version.CurrentStr,
                    PreviousVersion = version.Latest?.ToString(),
                    IsPrerelease = version.IsPrerelease,
                    IsPublicRelease = version.IsPublicRelease,
                },
                ProducedPackages = producedPackages,
                Dogfooded = dogfooded,
            };
            var dirtyBefore = git.GetDirtyFiles();
            var hookRan = await hookRunner.RunHookAsync("release", "post-release", hookContext, cancellationToken).ConfigureAwait(false);
            string[] hookUpdates = hookRan
                ? [.. git.GetDirtyFiles().Except(dirtyBefore, StringComparer.Ordinal)]
                : [];
            if (hookRan)
            {
                switch (hookUpdates.Length)
                {
                    case 0:
                        reporter.Info("The post-release hook modified no files.");
                        break;
                    case 1:
                        reporter.Info("The post-release hook modified 1 file.");
                        break;
                    default:
                        reporter.Info(string.Create(CultureInfo.InvariantCulture, $"The post-release hook modified {hookUpdates.Length} files."));
                        break;
                }
            }

            // Update in-tree references to packages produced by this release (dogfooding).
            // Must happen after pack (so the produced .nupkg files exist and the build ran against the
            // previously-published versions) and before push (so the rewrites travel with the release commit).
            // Goes into a separate commit so the tagged "Prepare release" commit reflects the actual built
            // state (which still references the previously-published versions); the post-release commit is
            // marked [skip ci] because the new packages aren't in the feed yet at push time.
            IReadOnlyList<string> selfReferenceUpdates = [];
            if (dogfooded)
            {
                selfReferenceUpdates = selfReferenceUpdater.UpdateReferences(producedPackages);
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
            }
            else
            {
                reporter.Info("Self-reference update skipped: option 'dogfood' is false.");
            }

            // Assemble the post-release commit from the self-reference rewrites and the hook's changes.
            string[] postReleaseUpdates = [.. selfReferenceUpdates.Union(hookUpdates, StringComparer.Ordinal)];
            if (postReleaseUpdates.Length > 0)
            {
                release.AddPostReleaseCommit($"Post-release updates for {version.CurrentStr} [skip ci]", postReleaseUpdates);
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
