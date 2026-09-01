// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Writes the package pins a run moves, in the files that declare them.
/// </summary>
/// <remarks>
/// <para>An MSBuild item is edited by splicing its version value, and a file-based app's directive by
/// splicing the text after its <c>@</c>. Neither file is round-tripped through a writer, so formatting,
/// comments and encoding survive byte for byte.</para>
/// <para>A pin is found by item type, id and version text, which is what tells apart two declarations of one
/// id conditioned per target framework. Two declarations that state the same version are one pin, and the
/// splice moves both, because MSBuild evaluated them as one.</para>
/// </remarks>
internal sealed class PackagePinWriter(IHomeDirectoryProvider home, IReporter reporter)
{
    /// <summary>
    /// Writes the pins that move.
    /// </summary>
    /// <param name="pins">What the run made of the package pins. Only the ones that move are written.</param>
    /// <exception cref="BuildFailedException">A file could not be read or written.</exception>
    public void Write(IReadOnlyList<PinResolution> pins)
    {
        Guard.IsNotNull(pins);
        foreach (var file in PinWriting.Moving(pins).GroupBy(static pin => pin.Pin.DeclaringFile))
        {
            var path = home.GetFullPath(file.Key);
            var items = file.Where(static pin => pin.Pin.ItemType is not null).ToArray();
            var directives = file.Where(static pin => pin.Pin.ItemType is null).ToArray();
            if (items.Length > 0)
            {
                var targets = PinWriting.TargetsOf(items);
                var itemTypes = items.Select(static pin => pin.Pin.ItemType!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                _ = MsBuildPinEditor.RewritePins(
                    path,
                    itemTypes,
                    pin => PinWriting.Restate(targets, pin.ItemType, pin.Id, pin.VersionText));
            }

            if (directives.Length > 0)
            {
                var targets = PinWriting.TargetsOf(directives);

                // The editor calls back only for directives that carry a version, so VersionText is never null here.
                _ = AppDirectiveEditor.RewriteVersions(
                    path,
                    directive => PinWriting.Restate(targets, itemType: null, directive.Id, directive.VersionText!));
            }

            reporter.Detail($"Stated the new versions in {file.Key}.");
        }
    }
}
