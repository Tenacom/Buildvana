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
    /// <exception cref="BuildFailedException">A file could not be read or written, or no longer states a pin
    /// the run moves.</exception>
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
                var rewritten = MsBuildPinEditor.RewritePins(
                    path,
                    itemTypes,
                    pin => PinWriting.Restate(targets, pin.ItemType, pin.Id, pin.VersionText));

                EnsureEdited(rewritten, path, "moves");
            }

            if (directives.Length > 0)
            {
                var targets = PinWriting.TargetsOf(directives);

                // The editor calls back only for directives that carry a version, so VersionText is never null here.
                var rewritten = AppDirectiveEditor.RewriteVersions(
                    path,
                    directive => PinWriting.Restate(targets, itemType: null, directive.Id, directive.VersionText!));

                EnsureEdited(rewritten, path, "moves");
            }

            reporter.Detail($"Stated the new versions in {file.Key}.");
        }
    }

    /// <summary>
    /// Removes pins, in the files that declare them.
    /// </summary>
    /// <param name="pins">The pins to remove. Only MSBuild items are removed: a directive is a reference,
    /// and a reference is removed by whoever wrote it.</param>
    /// <exception cref="BuildFailedException">A file could not be read or written, or no longer states a pin
    /// the run removes.</exception>
    public void Remove(IReadOnlyList<DependencyPin> pins)
    {
        Guard.IsNotNull(pins);
        var items = pins.Where(static pin => pin.ItemType is not null);
        foreach (var file in items.GroupBy(static pin => pin.DeclaringFile))
        {
            var path = home.GetFullPath(file.Key);
            var keys = PinWriting.KeysOf(file);
            var itemTypes = file.Select(static pin => pin.ItemType!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var removed = MsBuildPinEditor.RemovePins(
                path,
                itemTypes,
                pin => PinWriting.Names(keys, pin.ItemType, pin.Id, pin.VersionText));

            EnsureEdited(removed, path, "removes");
            reporter.Detail($"Removed the orphaned pins stated in {file.Key}.");
        }
    }

    // An editor says whether it wrote anything, and a writer that ignored the answer would report a run it
    // did not make. The file was read moments ago, so a refusal means it changed under us.
    private static void EnsureEdited(bool edited, string path, string verb)
        => BuildFailedException.ThrowIfNot(edited, $"{path} no longer states a package version this run {verb}.");
}
