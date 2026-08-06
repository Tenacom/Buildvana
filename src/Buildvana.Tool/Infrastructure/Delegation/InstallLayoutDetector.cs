// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Infrastructure.Delegation;

/// <summary>
/// Infers the <see cref="InstallLayout"/> the running tool executes from by matching its base directory against
/// the known .NET tool installation layouts.
/// </summary>
/// <remarks>
/// <para>The layouts are observed behavior, not contractual SDK surface:</para>
/// <list type="bullet">
///   <item><description>global and <c>--tool-path</c> installs execute from a store,
///   <c>…/.store/&lt;id&gt;/&lt;version&gt;/&lt;id&gt;/&lt;version&gt;/tools/&lt;tfm&gt;/&lt;rid&gt;/</c>;</description></item>
///   <item><description>manifest-run and <c>dnx</c>-run tools execute from an extracted NuGet package,
///   <c>…/&lt;id&gt;/&lt;version&gt;/tools/&lt;tfm&gt;/&lt;rid&gt;/</c>.</description></item>
/// </list>
/// <para>The store layout ends with the same <c>&lt;id&gt;/&lt;version&gt;/tools/&lt;tfm&gt;/&lt;rid&gt;</c>
/// suffix as the package layout, so the <c>.store</c> check must run first. Anything unrecognized degrades to
/// <see cref="InstallLayout.Unknown"/>.</para>
/// </remarks>
internal static class InstallLayoutDetector
{
    /// <summary>
    /// Infers the installation layout of the tool executing from <paramref name="baseDirectory"/>.
    /// </summary>
    /// <param name="baseDirectory">The tool's base directory, typically <c>AppContext.BaseDirectory</c>.</param>
    /// <param name="packageId">The tool's NuGet package ID.</param>
    /// <param name="version">The tool's version, in normalized form (no build metadata).</param>
    /// <returns>The inferred layout.</returns>
    public static InstallLayout Detect(string baseDirectory, string packageId, string version)
    {
        Guard.IsNotNullOrEmpty(baseDirectory);
        Guard.IsNotNullOrEmpty(packageId);
        Guard.IsNotNullOrEmpty(version);

        // Split on the platform's separators (there is no BCL API that enumerates path segments): on Windows
        // both '\' and '/' separate, everywhere else '\' is an ordinary file-name character and must not split.
        var segments = baseDirectory.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (ContainsStoreSequence(segments, packageId, version))
        {
            return InstallLayout.ToolStore;
        }

        if (EndsWithPackageSuffix(segments, packageId, version))
        {
            return InstallLayout.PackageCache;
        }

        return InstallLayout.Unknown;
    }

    // Matches the `.store/<id>/<version>` segment run of global and --tool-path installs, anywhere in the path.
    // Requiring the id and version after `.store` keeps a user directory that happens to be named `.store`
    // from turning an unrelated path into a store match.
    private static bool ContainsStoreSequence(string[] segments, string packageId, string version)
    {
        for (var i = 0; i + 2 < segments.Length; i++)
        {
            var isStore = Is(segments[i], ".store")
                && Is(segments[i + 1], packageId)
                && Is(segments[i + 2], version);
            if (isStore)
            {
                return true;
            }
        }

        return false;
    }

    // Matches the `<id>/<version>/tools/<tfm>/<rid>` tail of an extracted NuGet tool package. The tfm and rid
    // segments vary by tool and platform, so any value is accepted for them.
    private static bool EndsWithPackageSuffix(string[] segments, string packageId, string version)
        => segments.Length >= 5
            && Is(segments[^5], packageId)
            && Is(segments[^4], version)
            && Is(segments[^3], "tools");

    private static bool Is(string segment, string expected) => string.Equals(segment, expected, StringComparison.OrdinalIgnoreCase);
}
