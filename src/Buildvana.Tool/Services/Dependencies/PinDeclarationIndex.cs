// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Buildvana.Core;
using Buildvana.Core.HomeDirectory;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Answers, for a pin MSBuild evaluated, whether the file that declares it states its version itself.
/// </summary>
/// <remarks>
/// <para>An evaluated version says nothing about how the file states it. <c>Version="$(SerilogVersion)"</c>
/// evaluates to an exact version, and so does a version applied from elsewhere through
/// <c>PackageReference Update="..."</c>; rewriting either file would replace an indirection its author
/// wanted with a literal. Comparing the evaluated version with the text the file states tells the two
/// apart, and needs no property evaluation of bv's own.</para>
/// <para>A file is read once, however many pins it declares: one <c>Directory.Packages.props</c> answers
/// for all of them.</para>
/// </remarks>
internal sealed class PinDeclarationIndex(IHomeDirectoryProvider home, IReadOnlyList<string> itemTypes)
{
    private readonly Dictionary<string, IReadOnlyList<MsBuildPin>> _declarations = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether a file states a version itself.
    /// </summary>
    /// <param name="declaringFile">The path of the file, relative to the home directory.</param>
    /// <param name="itemType">The MSBuild item type the pin is declared as.</param>
    /// <param name="id">The package id.</param>
    /// <param name="versionText">The evaluated version.</param>
    /// <returns><see langword="true"/> if the file declares an item of that type and id whose version text
    /// is <paramref name="versionText"/>; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="BuildFailedException">The file could not be read.</exception>
    public bool StatesVersion(string declaringFile, string itemType, string id, string versionText)
    {
        Guard.IsNotNullOrEmpty(declaringFile);
        return Read(declaringFile).Any(declaration
            => string.Equals(declaration.ItemType, itemType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(declaration.Id, id, StringComparison.OrdinalIgnoreCase)

            // A Version child element carries the whitespace around its value, which is not part of it.
            && string.Equals(declaration.VersionText.Trim(), versionText, StringComparison.Ordinal));
    }

    private IReadOnlyList<MsBuildPin> Read(string declaringFile)
    {
        if (!_declarations.TryGetValue(declaringFile, out var declarations))
        {
            declarations = MsBuildPinEditor.ReadPins(home.GetFullPath(declaringFile), itemTypes);
            _declarations.Add(declaringFile, declarations);
        }

        return declarations;
    }
}
