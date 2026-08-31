// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

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
/// built-in SDKs. Such a directive keeps a pin declared elsewhere alive, which is orphan detection's
/// business, not this reader's.</para>
/// </remarks>
internal sealed class DirectivePinReader(IHomeDirectoryProvider home, BuildvanaConfig config)
{
    /// <summary>
    /// Walks the repository's file-based apps and reads the pins their directives state.
    /// </summary>
    /// <returns>The pins found, in walk order.</returns>
    /// <exception cref="BuildFailedException">A directory or file could not be read.</exception>
    public IReadOnlyList<DependencyPin> Read()
    {
        Guard.IsNotNull(config);
        var scope = FileBasedAppScope.Parse(config.FileBasedApps);
        var pins = new List<DependencyPin>();
        foreach (var relativePath in RepositoryFiles.CreateFinder(home).GetFiles())
        {
            if (!scope.Contains(relativePath))
            {
                continue;
            }

            foreach (var directive in AppDirectiveEditor.ReadDirectives(home.GetFullPath(relativePath)))
            {
                if (directive.VersionText is { } versionText && !BuildvanaFamily.Contains(directive.Id))
                {
                    pins.Add(DependencyPin.Create(ScopeOf(directive.Kind), directive.Id, versionText, relativePath));
                }
            }
        }

        return pins;
    }

    private static DependencyScope ScopeOf(AppDirectiveKind kind)
        => kind == AppDirectiveKind.Sdk ? DependencyScope.Sdks : DependencyScope.Packages;
}
