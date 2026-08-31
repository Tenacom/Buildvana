// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Buildvana.Core.Configuration;
using CommunityToolkit.Diagnostics;
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

    // Padding is stated as left, top, right and bottom: the first column is indented under the file that
    // declares its pins, and every column but the last is followed by a gap.
    private static GridColumn NewColumn(int left, int right) => new() { Padding = new Padding(left, 0, right, 0), NoWrap = true };

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
