// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text.Json;
using NuGet.Versioning;

namespace Buildvana.Tool.Infrastructure.Delegation;

/// <summary>
/// Probes the .NET SDK's local-tool resolver cache to tell whether a manifest-pinned tool version is already
/// installed — that is, whether <c>dotnet tool run</c> would resolve it without a prior <c>dotnet tool restore</c>.
/// </summary>
/// <remarks>
/// <para><c>dotnet tool run</c> resolves a manifest-pinned tool exclusively through this cache: it loads the
/// per-package file <c>&lt;dotnet home&gt;/.dotnet/toolResolverCache/1/&lt;package id&gt;</c>, looks for an
/// entry matching the pinned version, and checks that the executable the entry points to still exists (see
/// <c>LocalToolsCommandResolver</c> and <c>LocalToolsResolverCache</c> in the dotnet/sdk repository). Presence
/// of the package in the NuGet global packages folder is neither necessary nor sufficient. This probe mirrors
/// that check.</para>
/// <para>The cache is not contractual SDK surface, which is why the probe is only ever trusted in the skip
/// direction: a hit skips the restore, and a miss — including an unreadable or unrecognizable cache — falls
/// back to restoring, which is always correct. Format drift in a future SDK can cost an unnecessary restore,
/// never a broken run. The SDK itself reads the cache leniently and treats a corrupt file as empty ("it is not
/// the source of truth").</para>
/// <para>One known false-hit window: the SDK also matches entries on the bundled target framework of the SDK
/// that runs <c>dotnet tool run</c>, which this probe cannot know without spawning a process, so it matches on
/// version alone. Right after an SDK major upgrade the cache may hold only the old framework's entry: the probe
/// hits, the skipped restore turns out to be needed, and the delegated run fails with the CLI's own actionable
/// "Run 'dotnet tool restore'" message; one manual restore appends the new entry and heals the machine for
/// good. (Matching the framework against this process's runtime instead would misfire persistently on machines
/// with side-by-side SDKs, forcing a restore on every invocation — worse than the yearly hiccup.)</para>
/// </remarks>
/// <param name="cacheDirectory">The resolver cache directory (what <c>toolResolverCache</c> resolves to);
/// see <see cref="CreateDefault"/> for the real one.</param>
internal sealed class ToolResolverCacheProbe(string cacheDirectory)
{
    // The cache's on-disk format version, a path segment under the cache directory (LocalToolResolverCacheVersion
    // in the SDK).
    private const string CacheFormatVersion = "1";

    /// <summary>
    /// Creates a probe over the resolver cache <c>dotnet tool run</c> actually uses.
    /// </summary>
    /// <returns>The created probe.</returns>
    public static ToolResolverCacheProbe CreateDefault() => new(GetDefaultCacheDirectory());

    /// <summary>
    /// Tells whether the given tool package version is installed, judged the way <c>dotnet tool run</c> judges it.
    /// </summary>
    /// <param name="packageId">The tool's NuGet package ID.</param>
    /// <param name="version">The pinned version to look for.</param>
    /// <returns><see langword="true"/> when a cache entry for <paramref name="version"/> points to an existing
    /// executable; <see langword="false"/> otherwise, including when the cache cannot be read or understood.</returns>
    public bool IsInstalled(string packageId, NuGetVersion version)
    {
        try
        {
#pragma warning disable CA1308 // Normalize strings to uppercase — the SDK names cache files with the lowercased package ID; uppercasing would miss them
            var cacheFile = Path.Combine(cacheDirectory, CacheFormatVersion, packageId.ToLowerInvariant());
#pragma warning restore CA1308
            if (!File.Exists(cacheFile))
            {
                return false;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(cacheFile));
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var versionText = version.ToNormalizedString();
            foreach (var entry in document.RootElement.EnumerateArray())
            {
                if (IsMatchingEntry(entry, versionText))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    // Mirrors CliFolderPathCalculatorCore in the dotnet/sdk repository: DOTNET_CLI_HOME wins, then the
    // platform home variable (USERPROFILE on Windows, HOME elsewhere), then whatever the BCL considers the
    // user profile. Should every source come up empty, the result degrades to a relative path resolved
    // against the current directory; wherever that lands, a wrong cache directory only costs a restore.
    internal static string GetDefaultCacheDirectory()
    {
        var home = Environment.GetEnvironmentVariable("DOTNET_CLI_HOME");
        if (string.IsNullOrEmpty(home))
        {
            home = Environment.GetEnvironmentVariable(OperatingSystem.IsWindows() ? "USERPROFILE" : "HOME");
        }

        if (string.IsNullOrEmpty(home))
        {
            home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return Path.Combine(home, ".dotnet", "toolResolverCache");
    }

    // Release labels compare case-insensitively in NuGet version semantics, hence OrdinalIgnoreCase.
    private static bool IsMatchingEntry(JsonElement entry, string versionText)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var versionMatches = entry.TryGetProperty("Version", out var versionProperty)
            && versionProperty.ValueKind == JsonValueKind.String
            && string.Equals(versionProperty.GetString(), versionText, StringComparison.OrdinalIgnoreCase);
        if (!versionMatches)
        {
            return false;
        }

        var hasExecutablePath = entry.TryGetProperty("PathToExecutable", out var pathProperty)
            && pathProperty.ValueKind == JsonValueKind.String;
        return hasExecutablePath && File.Exists(pathProperty.GetString());
    }
}
