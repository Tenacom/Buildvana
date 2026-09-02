// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Buildvana.Core;
using Buildvana.Core.HomeDirectory;
using Buildvana.Runtime;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Reads the pins the repository's file-based apps state in their leading directive block: a
/// <c>#:package</c> directive carrying a version is a package pin, and a <c>#:sdk</c> directive carrying one
/// is a project SDK pin.
/// </summary>
/// <remarks>
/// <para>A directive is a package reference for all intents and purposes, and the file that holds it is the
/// file an update would edit. Discovery is textual: a file-based app is not part of the solution, so no
/// evaluation of the solution ever sees it.</para>
/// <para>A versionless directive names no version and is therefore no pin: <c>#:package Serilog</c> resolves
/// through central package management, and <c>#:sdk Buildvana.Sdk</c> through <c>msbuild-sdks</c> or the
/// built-in SDKs. A versionless <c>#:package</c> is read as a reference all the same, because it keeps the
/// central pin it resolves through from being an orphan.</para>
/// </remarks>
internal sealed class DirectivePinReader(IHomeDirectoryProvider home, BuildvanaConfig config)
{
    /// <summary>
    /// Walks the repository's file-based apps and reads what their directives state.
    /// </summary>
    /// <returns>The pins and the versionless references found, in walk order.</returns>
    /// <exception cref="BuildFailedException">A directory or file could not be read.</exception>
    public DirectivePins Read()
    {
        Guard.IsNotNull(config);
        var scope = FileBasedAppScope.Parse(config.FileBasedApps);
        var pins = new List<DependencyPin>();
        var references = new List<string>();
        var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relativePath in RepositoryFiles.CreateFinder(home).GetFiles())
        {
            if (!scope.Contains(relativePath))
            {
                continue;
            }

            foreach (var directive in AppDirectiveEditor.ReadDirectives(home.GetFullPath(relativePath)))
            {
                // A family directive is invisible here as everywhere else, whether or not it states a
                // version: bv self-update is the one command that moves the family.
                if (BuildvanaFamily.Contains(directive.Id))
                {
                    continue;
                }

                if (directive.VersionText is { } versionText)
                {
                    pins.Add(DependencyPin.Create(ScopeOf(directive.Kind), directive.Id, versionText, relativePath));
                }
                else if (directive.Kind == AppDirectiveKind.Package && named.Add(directive.Id))
                {
                    references.Add(directive.Id);
                }
            }
        }

        return new DirectivePins(pins, references);
    }

    private static DependencyScope ScopeOf(AppDirectiveKind kind)
        => kind == AppDirectiveKind.Sdk ? DependencyScope.Sdks : DependencyScope.Packages;
}
