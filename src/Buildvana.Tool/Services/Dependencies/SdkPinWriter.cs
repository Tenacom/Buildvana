// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Json;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Writes the MSBuild project SDK pins a run moves.
/// </summary>
/// <remarks>
/// <para>The scope spans two kinds of file: the <c>msbuild-sdks</c> section of <c>global.json</c>, whose
/// entries are string values, and the <c>#:sdk</c> directives of file-based apps. Each is spliced in place,
/// so nothing outside a version changes.</para>
/// </remarks>
internal sealed class SdkPinWriter(IHomeDirectoryProvider home, IJsonHelper jsonHelper, IReporter reporter)
{
    private const string MsBuildSdksSectionName = "msbuild-sdks";

    /// <summary>
    /// Writes the pins that move.
    /// </summary>
    /// <param name="pins">What the run made of the project SDK pins. Only the ones that move are written.</param>
    /// <exception cref="BuildFailedException">A file could not be read or written.</exception>
    public void Write(IReadOnlyList<PinResolution> pins)
    {
        Guard.IsNotNull(pins);
        foreach (var file in PinWriting.Moving(pins).GroupBy(static pin => pin.Pin.DeclaringFile))
        {
            var path = home.GetFullPath(file.Key);
            if (string.Equals(file.Key, GlobalJsonPinReader.RelativePath, StringComparison.Ordinal))
            {
                WriteGlobalJson(path, [.. file]);
            }
            else
            {
                var targets = PinWriting.TargetsOf(file);
                _ = AppDirectiveEditor.RewriteVersions(
                    path,
                    directive => PinWriting.Restate(targets, itemType: null, directive.Id, directive.VersionText!));
            }

            reporter.Detail($"Stated the new versions in {file.Key}.");
        }
    }

    private void WriteGlobalJson(string path, IReadOnlyList<PinResolution> pins)
    {
        var targets = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase);
        foreach (var pin in pins)
        {
            targets[pin.Pin.Id] = pin.Target!;
        }

        _ = jsonHelper.RewriteStringValues(
            path,
            (propertyPath, currentValue) => propertyPath is [MsBuildSdksSectionName, var id] && targets.TryGetValue(id, out var target)
                ? PinVersionText.Restate(currentValue, target)
                : null);
    }
}
