// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.IO;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Utilities;

/// <summary>
/// Provides helpers to inspect the artifacts produced by a build.
/// </summary>
internal static class ArtifactsHelper
{
    /// <summary>
    /// Discovers the packages produced by the current build by inspecting the <c>*.nupkg</c> files in the
    /// artifacts directory: each filename is expected to match the form <c>{Id}.{version}.nupkg</c>; files
    /// whose version suffix does not match <paramref name="version"/> are ignored.
    /// </summary>
    /// <param name="artifactsPath">The path of the directory containing the produced <c>*.nupkg</c> files.</param>
    /// <param name="version">The version being built, as a string.</param>
    /// <param name="reporter">Optional reporter. When provided, reports skipped files at <c>Detail</c>.</param>
    /// <returns>A dictionary mapping the ID of each produced package to <paramref name="version"/>.</returns>
    public static Dictionary<string, string> DiscoverProducedPackages(string artifactsPath, string version, IReporter? reporter = null)
    {
        Guard.IsNotNullOrEmpty(artifactsPath);
        Guard.IsNotNullOrEmpty(version);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!UserDirectory.Exists(artifactsPath))
        {
            return result;
        }

        var suffix = $".{version}.nupkg";
        foreach (var path in UserDirectory.EnumerateFiles(artifactsPath, "*.nupkg"))
        {
            var fileName = Path.GetFileName(path);
            if (!fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                reporter?.Detail($"Produced packages: skipping '{fileName}' (version does not match '{version}').");
                continue;
            }

            var id = fileName[..^suffix.Length];
            result[id] = version;
        }

        return result;
    }
}
