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
/// </remarks>
internal sealed class DependencyReportRenderer(IAnsiConsole console, EffectivePolicyResolver policies)
{
    private static readonly string[] UpdateHeaders =
        ["package", "pinned", "policy", "target", "latest stable", "latest preview", "notes"];

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

    // Padding is stated as left, top, right and bottom: the first column is indented under the file that
    // declares its pins, and every column but the last is followed by a gap.
    private static GridColumn NewColumn(int left, int right) => new() { Padding = new Padding(left, 0, right, 0), NoWrap = true };

    private static string[] UpdateRowOf(PinResolution resolution)
        =>
        [
            resolution.Pin.Id,
            resolution.Pin.VersionText,
            resolution.Policy.ToString(),
            VersionText(resolution.Target),
            VersionText(resolution.LatestStable),
            VersionText(resolution.LatestPreview),
            resolution.Note,
        ];

    private static string[] UpdateRowOfNetSdk(NetSdkResolution resolution)
        =>
        [
            "(the .NET SDK)",
            resolution.Pin.VersionText,
            resolution.Policy.ToString(),
            VersionText(resolution.Target),
            VersionText(resolution.LatestStable),
            VersionText(resolution.LatestPreview),
            resolution.Note,
        ];

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
        WriteRows([("(the .NET SDK)", pin.VersionText, policy.ToString(), PinNotes.ForNetSdk(pin, policy))]);
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
            WriteRows([.. file.Select(RowOf)]);
        }
    }

    private (string Id, string Version, string Policy, string Note) RowOf(DependencyPin pin)
    {
        var policy = policies.Resolve(pin);
        return (pin.Id, pin.VersionText, policy.ToString(), PinNotes.For(pin, policy));
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
            WriteUpdateRows([UpdateRowOfNetSdk(resolution)]);
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
            WriteUpdateRows([.. file.Select(UpdateRowOf)]);
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

    // The notes column exists only where a row has something to say, and the header names what a reader is
    // looking at: six versions of the same shape need saying apart.
    private void WriteUpdateRows(IReadOnlyList<string[]> rows)
    {
        var visible = rows.Any(static row => row[^1].Length > 0) ? UpdateHeaders.Length : UpdateHeaders.Length - 1;
        var grid = new Grid();
        for (var index = 0; index < visible; index++)
        {
            grid.AddColumn(NewColumn(index == 0 ? 4 : 0, index == visible - 1 ? 0 : 2));
        }

        grid.AddRow([.. UpdateHeaders.Take(visible).Select(static header => $"[dim]{header}[/]")]);
        foreach (var row in rows)
        {
            grid.AddRow([.. row.Take(visible).Select(Markup.Escape)]);
        }

        console.Write(grid);
    }

    // A row is markup, and a version is data: `[13.0.4]` names one version, and Spectre would read it as a
    // style tag. The notes column exists only where a row has something to say, so that a report of pins
    // nothing is wrong with carries no empty column and no trailing blanks.
    private void WriteRows(IReadOnlyList<(string Id, string Version, string Policy, string Note)> rows)
    {
        var hasNotes = rows.Any(static row => row.Note.Length > 0);
        var grid = new Grid();
        grid.AddColumn(NewColumn(4, 2));
        grid.AddColumn(NewColumn(0, 2));
        grid.AddColumn(NewColumn(0, hasNotes ? 2 : 0));
        if (hasNotes)
        {
            grid.AddColumn(new GridColumn { Padding = new Padding(0, 0, 0, 0) });
        }

        foreach (var row in rows)
        {
            string[] cells = [Markup.Escape(row.Id), Markup.Escape(row.Version), Markup.Escape(row.Policy)];
            grid.AddRow(hasNotes ? [.. cells, Markup.Escape(row.Note)] : cells);
        }

        console.Write(grid);
    }

    private void WriteHeading(string heading)
    {
        console.WriteLine();
        console.MarkupLineInterpolated($"[bold]{heading}[/]");
    }
}
