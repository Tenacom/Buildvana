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

    // What a reader must know about a pin beyond its version and its policy: that nothing will move it, and
    // why; or that it states a prerelease under a policy that takes only stable versions, which no update
    // undoes and no update follows any further.
    private static string NoteOf(DependencyPin pin, PackageUpdatePolicy policy)
    {
        var note = UnmanagedNote(pin.Management);
        if (note.Length > 0)
        {
            return note;
        }

        return pin.Version is { IsPrerelease: true } && !policy.AllowPrerelease
            ? "a prerelease under a policy that takes only stable versions; end the policy with '-' to follow the prerelease line"
            : string.Empty;
    }

    // The allowPrerelease setting is derived state: it must say what the policy says, and an apply run
    // writes it. Offline, a disagreement is worth a word of its own, since nothing else here shows it.
    private static string NetSdkNote(NetSdkPin pin, NetSdkUpdatePolicy policy)
    {
        var note = UnmanagedNote(pin.Management);
        if (note.Length > 0)
        {
            return note;
        }

        return pin.AllowPrerelease == policy.AllowPrerelease
            ? string.Empty
            : $"global.json states allowPrerelease as {Stated(pin.AllowPrerelease)}, where the policy says {policy.AllowPrerelease}";
    }

    private static string UnmanagedNote(PinManagement management)
        => management switch
        {
            PinManagement.Managed => string.Empty,
            PinManagement.BracketExactVersion => "not managed: one version in brackets; write it without them to have bv move it",
            PinManagement.VersionRange => "not managed: a version range decides what resolves",
            PinManagement.FloatingVersion => "not managed: a floating version resolves anew at every restore",
            PinManagement.UnreadableVersion => "not managed: NuGet reads this as neither a version nor a range",
            PinManagement.VersionOverride => "not managed: VersionOverride departs from the central pin on purpose",
            _ => "not managed: the file states the version through a property, not as a literal",
        };

    private static string Stated(bool? value) => value?.ToString() ?? "unstated";

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
        WriteRows([("(the .NET SDK)", pin.VersionText, policy.ToString(), NetSdkNote(pin, policy))]);
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
        return (pin.Id, pin.VersionText, policy.ToString(), NoteOf(pin, policy));
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
