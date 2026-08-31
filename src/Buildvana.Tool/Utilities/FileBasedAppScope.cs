// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Buildvana.Core.IO;
using Buildvana.Core.IO.Gitignore;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Utilities;

/// <summary>
/// The set of files a repository declares to be file-based C# apps, as gitignore-syntax patterns; see
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
    private readonly GitignoreFile _patterns;
    private readonly bool _ignoresCase;

    private FileBasedAppScope(GitignoreFile patterns, bool ignoresCase)
    {
        _patterns = patterns;
        _ignoresCase = ignoresCase;
    }

    /// <summary>
    /// Reads a scope from its patterns.
    /// </summary>
    /// <param name="patterns">The gitignore-syntax patterns, in the order they are stated: the last one that
    /// matches decides, as in a <c>.gitignore</c> file.</param>
    /// <returns>The scope.</returns>
    public static FileBasedAppScope Parse(IReadOnlyList<string> patterns)
    {
        Guard.IsNotNull(patterns);
        return new FileBasedAppScope(GitignoreFile.Parse(patterns), CaseSensitivityMode.SystemDefault.IgnoresCase());
    }

    /// <summary>
    /// Determines whether a file is one of the repository's file-based apps.
    /// </summary>
    /// <param name="relativePath">The path of the file, relative to the home directory, with forward slashes.</param>
    /// <returns><see langword="true"/> if the file is a file-based app; otherwise, <see langword="false"/>.</returns>
    public bool Contains(string relativePath)
    {
        Guard.IsNotNull(relativePath);
        if (!string.Equals(Path.GetExtension(relativePath), ".cs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Mirror of the gitignore walk with "select" in place of "ignore": a matched directory selects its
        // whole subtree, and a file needs a pattern of its own only when no ancestor directory matched.
        var components = relativePath.Split('/');
        for (var count = 1; count <= components.Length; count++)
        {
            var isDirectory = count < components.Length;
            if (_patterns.Evaluate(components.AsSpan(0, count), isDirectory, _ignoresCase) == GitignoreDecision.Ignore)
            {
                return true;
            }
        }

        return false;
    }
}
