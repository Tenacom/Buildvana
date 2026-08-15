// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.IO;
using Buildvana.Core.Configuration;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Diagnostics;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Versioning;
using Buildvana.Runtime;

namespace Buildvana.Sdk.Tasks;

partial class ComputeVersion
{
    private static readonly ConcurrentDictionary<string, CachedVersion> Cache = new(StringComparer.Ordinal);

    private static CachedVersion GetOrComputeVersion(string homeDirectory, IReporter reporter)
    {
        homeDirectory = Path.GetFullPath(homeDirectory);
        var fingerprint = ComputeFingerprint(homeDirectory);
        if (fingerprint is null)
        {
            return ComputeCore(homeDirectory, reporter, null);
        }

        if (Cache.TryGetValue(homeDirectory, out var cached) && cached.Fingerprint == fingerprint)
        {
            return cached;
        }

        var computed = ComputeCore(homeDirectory, reporter, fingerprint);
        Cache[homeDirectory] = computed;
        return computed;
    }

    // The computed version is reported at detail level, so it stays out of the way at the verbosities a
    // build is normally run at. There is nothing here for a user to act upon: the version reaches them
    // through the artifacts it stamps, and this line is emitted once per cache miss, i.e. once per process
    // taking part in the build - a count that reflects MSBuild's node topology rather than anything about
    // the repository. What it is good for is following a build closely enough to see when, and how often,
    // the version is actually computed.
    private static CachedVersion ComputeCore(string homeDirectory, IReporter reporter, string? fingerprint)
    {
        var home = new FixedHomeDirectoryProvider(homeDirectory);
        var calculator = new VersionCalculator(
            home,
            new VersioningSettings(new BuildvanaConfigProvider(home).Config),
            new GitHeightCalculator(VersionFile.FileName));
        var version = calculator.Calculate();
        var publicity = version.IsPublicRelease ? "public release" : "not a public release";
        reporter.Detail(FormattableString.Invariant($"Computed version {version.SemVer} (height {version.Height}, {publicity})."));
        return new CachedVersion(fingerprint, version);
    }

    // The fingerprint captures everything the computed version depends on: the working-tree VERSION file,
    // the configuration file, and the repository state (the height walk only sees commits reachable from HEAD).
    // A null fingerprint disables caching, letting VersionCalculator surface the proper error.
    private static string? ComputeFingerprint(string homeDirectory)
    {
        try
        {
            var versionPath = Path.Combine(homeDirectory, VersionFile.FileName);
            if (!File.Exists(versionPath))
            {
                return null;
            }

            var repositoryStateToken = GitHeightCalculator.TryGetRepositoryStateToken(homeDirectory);
            if (repositoryStateToken is null)
            {
                return null;
            }

            var buildvanaDirectory = Path.Combine(homeDirectory, WellKnownPaths.BuildvanaDirectory);
            string?[] parts =
            [
                repositoryStateToken,
                File.ReadAllText(versionPath),
                ReadOptionalFile(Path.Combine(homeDirectory, BuildvanaConfig.JsonFileName)),
                ReadOptionalFile(Path.Combine(homeDirectory, BuildvanaConfig.JsoncFileName)),
                ReadOptionalFile(Path.Combine(buildvanaDirectory, BuildvanaConfig.JsonFileName)),
                ReadOptionalFile(Path.Combine(buildvanaDirectory, BuildvanaConfig.JsoncFileName)),
            ];
            return string.Join('\0', parts);
        }
        catch (Exception e) when (!e.IsFatalException)
        {
            return null;
        }
    }

    private static string? ReadOptionalFile(string path) => File.Exists(path) ? File.ReadAllText(path) : null;
}
