// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Diagnostics;
using NuGet.Versioning;
using Spectre.Console;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Writes what a repository pins, scope by scope, with the policy governing every pin.
/// </summary>
/// <remarks>
/// <para>The report is the command's deliverable, not narration: it goes to the console whatever the
/// verbosity, like the report of <c>bv version show</c>.</para>
/// <para>Pins are grouped by the file that declares them, because that file is what an update would edit
/// and what a reader would open. Nothing is hidden: a pin nothing can move is listed with the reason, and a
/// selected scope with no pins says that it has none.</para>
/// <para>What a pin's state is travels in words. A colour may carry it as well, and never alone.</para>
/// <para>A pin is one line, and that line never breaks an id or a version. A column layout cannot promise
/// as much: it divides the console width among the columns, and at the eighty columns of a CI log a preview
/// version is wider than the share it gets. What a reader must know beyond the line goes under it, indented,
/// where it wraps as the prose it is.</para>
/// </remarks>
internal sealed class DependencyReportRenderer(IAnsiConsole console, EffectivePolicyResolver policies)
{
    /// <summary>
    /// Writes the report of what the selected scopes pin.
    /// </summary>
    /// <param name="inventory">What the repository pins.</param>
    /// <param name="scopes">The scopes the invocation selected.</param>
    public void Write(DependencyInventory inventory, IReadOnlySet<DependencyScope> scopes)
    {
        Guard.IsNotNull(inventory);
        Guard.IsNotNull(scopes);
        if (scopes.Contains(DependencyScope.NetSdk))
        {
            WriteNetSdk(inventory.NetSdk);
        }

        if (scopes.Contains(DependencyScope.Sdks))
        {
            WriteScope("MSBuild project SDKs", inventory.Sdks);
        }

        if (scopes.Contains(DependencyScope.Tools))
        {
            WriteScope(".NET local tools", inventory.Tools);
        }

        if (scopes.Contains(DependencyScope.Packages))
        {
            WritePackages(inventory.Packages);
        }
    }

    /// <summary>
    /// Writes the report of what a run made of the selected scopes.
    /// </summary>
    /// <param name="resolution">What the run made of every pin.</param>
    /// <param name="scopes">The scopes the invocation selected.</param>
    /// <param name="listUpToDate">Whether the report lists the pins that are already at their target.</param>
    /// <param name="applied">Whether the run made the changes it reports, as opposed to foreseeing them.</param>
    public void WriteUpdate(
        DependencyResolution resolution,
        IReadOnlySet<DependencyScope> scopes,
        bool listUpToDate,
        bool applied)
    {
        Guard.IsNotNull(resolution);
        Guard.IsNotNull(scopes);
        if (scopes.Contains(DependencyScope.NetSdk))
        {
            WriteNetSdkUpdate(resolution.NetSdk, listUpToDate, applied);
        }

        if (scopes.Contains(DependencyScope.Sdks))
        {
            WriteScopeUpdate("MSBuild project SDKs", resolution.Sdks, listUpToDate, applied);
        }

        if (scopes.Contains(DependencyScope.Tools))
        {
            WriteScopeUpdate(".NET local tools", resolution.Tools, listUpToDate, applied);
        }

        if (scopes.Contains(DependencyScope.Packages))
        {
            WritePackagesUpdate(resolution.Packages, listUpToDate, applied);
        }
    }

    private static string UpdateLineOf(PinResolution resolution)
    {
        var outcome = Outcome(resolution.State, resolution.Target);
        var latest = Latest(resolution.LatestStable, resolution.LatestPreview);
        return $"{resolution.Pin.Id} {resolution.Pin.VersionText} ({resolution.Policy}) -> {outcome}{latest}";
    }

    private static string UpdateLineOfNetSdk(NetSdkResolution resolution)
    {
        var outcome = Outcome(resolution.State, resolution.Target);
        var latest = Latest(resolution.LatestStable, resolution.LatestPreview);
        return $"(the .NET SDK) {resolution.Pin.VersionText} ({resolution.Policy}) -> {outcome}{latest}";
    }

    // What the arrow points at: the version a pin moves to, or the words that say why it moves nowhere. The
    // last state is Held, which is resolution finding nothing the policy allows.
    private static string Outcome(PinResolutionState state, NuGetVersion? target)
        => state switch
        {
            PinResolutionState.Updated => VersionText(target),
            PinResolutionState.UpToDate => "up to date",
            PinResolutionState.Disabled => "disabled",
            PinResolutionState.Unmanaged => "not managed",
            PinResolutionState.Skipped => "not selected",
            _ => "held",
        };

    // What the sources have beyond the pin, stable first, and nothing at all where they have nothing to add.
    private static string Latest(NuGetVersion? stable, NuGetVersion? preview)
        => (stable, preview) switch
        {
            (null, null) => string.Empty,
            (not null, null) => $" (latest: {VersionText(stable)})",
            (null, not null) => $" (latest: {VersionText(preview)})",
            _ => $" (latest: {VersionText(stable)}, {VersionText(preview)})",
        };

    private static string VersionText(NuGetVersion? version) => version?.ToNormalizedString() ?? string.Empty;

    private static string Pins(int count)
        => count switch
        {
            0 => "No pin",
            1 => "1 pin",
            _ => $"{count} pins",
        };

    private void WriteNetSdk(NetSdkPin? pin)
    {
        WriteHeading(".NET SDK");
        if (pin is null)
        {
            console.MarkupLineInterpolated($"  {GlobalJsonPinReader.RelativePath} pins no .NET SDK version");
            return;
        }

        var policy = policies.ResolveNetSdk();
        console.MarkupLineInterpolated($"  {GlobalJsonPinReader.RelativePath}");
        WritePinLine($"(the .NET SDK) {pin.VersionText} ({policy})", PinNotes.ForNetSdk(pin, policy));
    }

    private void WriteScope(string heading, IReadOnlyList<DependencyPin> pins)
    {
        WriteHeading(heading);
        WriteFileGroups(pins);
    }

    // An additional group is a section of its own, under the caption its configuration gives it: its pins
    // are package pins, and the group is how a reader recognizes them.
    private void WritePackages(IReadOnlyList<DependencyPin> pins)
    {
        var ungrouped = pins.Where(static pin => pin.GroupCaption is null).ToArray();
        var groups = pins.Where(static pin => pin.GroupCaption is not null).GroupBy(static pin => pin.GroupCaption).ToArray();
        WriteHeading("NuGet packages");

        // "nothing pinned" is said of the scope, not of one heading: with a group's pins listed below, the
        // scope has pins, and the heading of what belongs to no group says nothing at all.
        if (ungrouped.Length > 0 || groups.Length == 0)
        {
            WriteFileGroups(ungrouped);
        }

        foreach (var group in groups)
        {
            WriteHeading($"NuGet packages: {group.Key}");
            WriteFileGroups([.. group]);
        }
    }

    private void WriteFileGroups(IReadOnlyList<DependencyPin> pins)
    {
        if (pins.Count == 0)
        {
            console.MarkupLine("  nothing pinned");
            return;
        }

        foreach (var file in pins.GroupBy(static pin => pin.DeclaringFile))
        {
            console.MarkupLineInterpolated($"  {file.Key}");
            foreach (var pin in file)
            {
                WritePin(pin);
            }
        }
    }

    private void WritePin(DependencyPin pin)
    {
        var policy = policies.Resolve(pin);
        WritePinLine($"{pin.Id} {pin.VersionText} ({policy})", PinNotes.For(pin, policy));
    }

    // The baseline is one row, listed when it has news: a version to move to, or an allowPrerelease setting
    // an apply run will write.
    private void WriteNetSdkUpdate(NetSdkResolution? resolution, bool listUpToDate, bool applied)
    {
        WriteHeading(".NET SDK");
        if (resolution is null)
        {
            console.MarkupLineInterpolated($"  {GlobalJsonPinReader.RelativePath} pins no .NET SDK version");
            return;
        }

        var changes = resolution.State == PinResolutionState.Updated || resolution.WritesAllowPrerelease;
        var isUpToDate = resolution is { State: PinResolutionState.UpToDate, WritesAllowPrerelease: false };
        if (listUpToDate || !isUpToDate)
        {
            console.MarkupLineInterpolated($"  {GlobalJsonPinReader.RelativePath}");
            WritePinLine(UpdateLineOfNetSdk(resolution), resolution.Note);
        }

        WriteCounts(changes ? 1 : 0, isUpToDate ? 1 : 0, listUpToDate, applied);
    }

    private void WriteScopeUpdate(string heading, IReadOnlyList<PinResolution> pins, bool listUpToDate, bool applied)
    {
        WriteHeading(heading);
        WriteUpdateFileGroups(pins, listUpToDate);
        WriteScopeCounts(pins, listUpToDate, applied);
    }

    // An additional group is a section of its own here as well, under the caption its configuration gives it.
    private void WritePackagesUpdate(IReadOnlyList<PinResolution> pins, bool listUpToDate, bool applied)
    {
        var ungrouped = pins.Where(static pin => pin.Pin.GroupCaption is null).ToArray();
        var groups = pins.Where(static pin => pin.Pin.GroupCaption is not null).GroupBy(static pin => pin.Pin.GroupCaption).ToArray();
        WriteHeading("NuGet packages");
        if (ungrouped.Length > 0 || groups.Length == 0)
        {
            WriteUpdateFileGroups(ungrouped, listUpToDate);
            WriteScopeCounts(ungrouped, listUpToDate, applied);
        }

        foreach (var group in groups)
        {
            WriteHeading($"NuGet packages: {group.Key}");
            WriteUpdateFileGroups([.. group], listUpToDate);
            WriteScopeCounts([.. group], listUpToDate, applied);
        }
    }

    private void WriteUpdateFileGroups(IReadOnlyList<PinResolution> pins, bool listUpToDate)
    {
        if (pins.Count == 0)
        {
            console.MarkupLine("  nothing pinned");
            return;
        }

        // A pin no filter named has no news either: the report leaves it out with the up-to-date ones.
        var listed = pins.Where(pin => listUpToDate || pin.State is not (PinResolutionState.UpToDate or PinResolutionState.Skipped))
            .ToArray();
        foreach (var file in listed.GroupBy(static pin => pin.Pin.DeclaringFile))
        {
            console.MarkupLineInterpolated($"  {file.Key}");
            foreach (var pin in file)
            {
                WritePinLine(UpdateLineOf(pin), pin.Note);
            }
        }
    }

    private void WriteScopeCounts(IReadOnlyList<PinResolution> pins, bool listUpToDate, bool applied)
    {
        if (pins.Count == 0)
        {
            return;
        }

        var changed = pins.Count(static pin => pin.State == PinResolutionState.Updated);
        var upToDate = pins.Count(static pin => pin.State == PinResolutionState.UpToDate);
        WriteCounts(changed, upToDate, listUpToDate, applied);
    }

    private void WriteCounts(int changed, int upToDate, bool listUpToDate, bool applied)
    {
        console.MarkupLineInterpolated($"  {Pins(changed)} {(applied ? "changed" : "would change")}.");
        if (!listUpToDate && upToDate > 0)
        {
            console.MarkupLineInterpolated($"  {Pins(upToDate)} up to date, not listed.");
        }
    }

    // A line is markup, and a version is data: `[13.0.4]` names one version, and Spectre would read it as a
    // style tag. Both halves go in as interpolated values, which Spectre escapes for us. A pin with nothing
    // to say beyond its line gets no second line, so a report of pins nothing is wrong with carries none.
    // The two lines are written as lines, not as an indented block: Spectre pads every line of a block to
    // the block's width, and a report full of trailing spaces is a poor thing to paste into an issue. The
    // cost is that a line too long for the console continues at column 0.
    private void WritePinLine(string line, string note)
    {
        console.MarkupLineInterpolated($"    {line}");
        if (note.Length > 0)
        {
            console.MarkupLineInterpolated($"      {note}");
        }
    }

    private void WriteHeading(string heading)
    {
        console.WriteLine();
        console.MarkupLineInterpolated($"[bold]{heading}[/]");
    }
}
