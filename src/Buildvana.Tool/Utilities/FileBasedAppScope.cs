// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Utilities;

/// <summary>
/// The set of files a repository declares to be file-based C# apps; see
/// <see cref="Buildvana.Runtime.BuildvanaConfig.FileBasedApps"/>.
/// </summary>
/// <remarks>
/// <para>Reading the <c>#:</c> directives of every <c>.cs</c> file of a repository would make the cost of a
/// scan grow with the whole source tree, and would call a source file an app on the strength of a comment
/// at its top. The declared scope answers both: a <c>.cs</c> file inside it is an app, and one outside it is
/// out of scope by the repository's own statement.</para>
/// </remarks>
internal sealed class FileBasedAppScope
{
    private readonly PathPatternSet _patterns;

    private FileBasedAppScope(PathPatternSet patterns) => _patterns = patterns;

    /// <summary>
    /// Reads a scope from its patterns.
    /// </summary>
    /// <param name="patterns">The gitignore-syntax patterns selecting the repository's file-based apps.</param>
    /// <returns>The scope.</returns>
    public static FileBasedAppScope Parse(IReadOnlyList<string> patterns) => new(PathPatternSet.Parse(patterns));

    /// <summary>
    /// Determines whether a file is one of the repository's file-based apps.
    /// </summary>
    /// <param name="relativePath">The path of the file, relative to the home directory, with forward slashes.</param>
    /// <returns><see langword="true"/> if the file is a file-based app; otherwise, <see langword="false"/>.</returns>
    public bool Contains(string relativePath)
    {
        Guard.IsNotNull(relativePath);

        // The extension picks the language; the declared patterns say which .cs files are apps rather than
        // project sources.
        return string.Equals(Path.GetExtension(relativePath), ".cs", StringComparison.OrdinalIgnoreCase)
            && _patterns.Contains(relativePath);
    }
}
