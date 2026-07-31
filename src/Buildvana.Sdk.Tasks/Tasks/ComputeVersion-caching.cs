// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.IO;
using Buildvana.Core.Configuration;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Versioning;
using Buildvana.Sdk.Internal;

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

    private static CachedVersion ComputeCore(string homeDirectory, IReporter reporter, string? fingerprint)
    {
        var service = new VersioningService(
            reporter,
            new FixedHomeDirectoryProvider(homeDirectory),
            new VersioningSettings(BuildvanaConfigLoader.Load(homeDirectory)),
            new GitHeightCalculator(VersionFile.FileName));
        return new CachedVersion(
            fingerprint,
            service.SimpleVersion,
            service.SemVer,
            service.AssemblyVersion,
            service.FileVersion,
            service.InformationalVersion,
            service.IsPublicRelease,
            service.IsPrerelease,
            service.CommitId ?? string.Empty,
            service.Height);
    }

    // The fingerprint captures everything the computed version depends on: the working-tree VERSION file,
    // the configuration file, and the repository state (the height walk only sees commits reachable from HEAD).
    // A null fingerprint disables caching, letting VersioningService surface the proper error.
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

            string?[] parts =
            [
                repositoryStateToken,
                File.ReadAllText(versionPath),
                ReadOptionalFile(Path.Combine(homeDirectory, "buildvana.json")),
                ReadOptionalFile(Path.Combine(homeDirectory, "buildvana.jsonc")),
            ];
            return string.Join('\0', parts);
        }
        catch (Exception e) when (!e.IsFatalException())
        {
            return null;
        }
    }

    private static string? ReadOptionalFile(string path) => File.Exists(path) ? File.ReadAllText(path) : null;
}
