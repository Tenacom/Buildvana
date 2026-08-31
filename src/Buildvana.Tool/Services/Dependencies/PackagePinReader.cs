// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Dependencies;
using Buildvana.Core.HomeDirectory;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Turns the package items MSBuild evaluated into the pins of the <c>packages</c> scope.
/// </summary>
/// <remarks>
/// <para>A pin is what one file says about one id: the same item evaluated by ten projects, as a
/// <c>Directory.Build.props</c> reference is, is one pin, and one file stating one id twice — once per
/// target framework, at two versions — is two pins, told apart by their version text.</para>
/// <para>Two kinds of item never become pins. An implicitly defined reference belongs to the SDK that
/// injected it, not to the repository. A family pin belongs to <c>bv self-update</c>.</para>
/// <para>A pin is managed only when its own declaring file states its version as a literal. The evaluated
/// version of <c>Version="$(SerilogVersion)"</c> is exact, and rewriting the file would replace an
/// indirection its author wanted with a literal; the same holds for a version applied from elsewhere
/// through <c>PackageReference Update="..."</c>. Comparing the evaluated version with what the file says
/// tells the two apart, and needs no property evaluation of bv's own.</para>
/// </remarks>
internal sealed class PackagePinReader(IHomeDirectoryProvider home, IReporter reporter)
{
    private readonly PinDeclarationIndex _declarations = new(home, PackageItemTypes.BuiltIn);

    /// <summary>
    /// Reads the pins of the <c>packages</c> scope out of what the pin dump target wrote.
    /// </summary>
    /// <param name="dumps">The dumps, one per evaluation.</param>
    /// <returns>The pins, ordered by declaring file and then by id.</returns>
    /// <exception cref="BuildFailedException">A declaring file could not be read.</exception>
    public IReadOnlyList<DependencyPin> Read(IReadOnlyList<PackagePinDump> dumps)
    {
        Guard.IsNotNull(dumps);

        // What makes two evaluated items one pin: the file that declares them, the item type, the id, and
        // the version text, which is what tells two target-framework-conditioned declarations apart.
        var pins = new Dictionary<(string DeclaringFile, string ItemType, string Id, string VersionText), DependencyPin>();
        var outsideHome = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in dumps.SelectMany(static dump => dump.Items))
        {
            AddPin(pins, outsideHome, item);
        }

        ReportItemsOutsideHome(outsideHome);
        return [.. pins.Values.OrderBy(static pin => pin.DeclaringFile, StringComparer.Ordinal)
            .ThenBy(static pin => pin.Id, StringComparer.OrdinalIgnoreCase)];
    }

    // A pin lives where it is declared, so an item declared outside the repository is nobody's to edit: a
    // Directory.Packages.props above the home directory, or one a package brought in. It is reported once
    // per file, and only where a reader asked for detail: the default report is about what the repository
    // can act on.
    private void ReportItemsOutsideHome(HashSet<string> files)
    {
        foreach (var file in files.Order(StringComparer.Ordinal))
        {
            reporter.Detail($"Package items declared in '{file}' are outside the repository and are left alone.");
        }
    }

    private void AddPin(
        Dictionary<(string DeclaringFile, string ItemType, string Id, string VersionText), DependencyPin> pins,
        HashSet<string> outsideHome,
        PackagePinDumpItem item)
    {
        if (item.IsImplicitlyDefined || BuildvanaFamily.Contains(item.Id))
        {
            return;
        }

        // A reference under central package management carries no version of its own, and is a reference to
        // a pin declared elsewhere rather than a pin. One carrying VersionOverride is a pin, and an
        // unmanaged one: the override is a decision about one project, where a policy is about one id.
        var versionText = item.Version ?? item.VersionOverride;
        if (versionText is null)
        {
            return;
        }

        if (!home.TryGetRelativePath(item.DefiningProjectFullPath, out var declaringFile))
        {
            _ = outsideHome.Add(item.DefiningProjectFullPath);
            return;
        }

        var key = (declaringFile, item.ItemType, item.Id, versionText);
        if (pins.ContainsKey(key))
        {
            return;
        }

        var pin = DependencyPin.Create(DependencyScope.Packages, item.Id, versionText, declaringFile) with
        {
            ItemType = item.ItemType,
            MetadataPolicy = item.UpdatePolicy,
        };

        pins.Add(key, ClassifyDeclaration(pin, item));
    }

    private DependencyPin ClassifyDeclaration(DependencyPin pin, PackagePinDumpItem item)
    {
        if (item.VersionOverride is not null)
        {
            return pin with { Management = PinManagement.VersionOverride };
        }

        if (pin.Management != PinManagement.Managed
            || _declarations.StatesVersion(pin.DeclaringFile, pin.ItemType!, pin.Id, pin.VersionText))
        {
            return pin;
        }

        return pin with { Management = PinManagement.IndirectVersion };
    }
}
