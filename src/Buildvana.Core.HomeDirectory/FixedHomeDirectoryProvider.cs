// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.HomeDirectory;

/// <summary>
/// An <see cref="IHomeDirectoryProvider"/> that wraps a caller-supplied home directory path.
/// </summary>
/// <remarks>
/// <para>Intended for hosts that already know the home directory and do not need discovery, e.g. compiled MSBuild
/// tasks consuming the SDK's <c>$(HomeDirectory)</c> property as a task parameter.</para>
/// </remarks>
public sealed class FixedHomeDirectoryProvider : HomeDirectoryProvider
{
    private readonly string _homeDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="FixedHomeDirectoryProvider"/> class.
    /// </summary>
    /// <param name="homeDirectory">The absolute path of the home directory.</param>
    public FixedHomeDirectoryProvider(string homeDirectory)
    {
        Guard.IsNotNullOrEmpty(homeDirectory);

        homeDirectory = Path.GetFullPath(homeDirectory);
        BuildFailedException.ThrowIfNot(Directory.Exists(homeDirectory), $"The specified home directory '{homeDirectory}' does not exist.");
        _homeDirectory = homeDirectory;
    }

    /// <inheritdoc />
    protected override string Resolve() => _homeDirectory;
}
