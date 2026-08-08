// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;
using Buildvana.Core.HomeDirectory;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Infrastructure;

/// <summary>
/// Decorates a home directory provider, making the home directory the process's current directory
/// at the moment of discovery. From that point on, every relative path in the process — including
/// relative paths in forwarded arguments — resolves against the home directory no matter where
/// <c>bv</c> was invoked from, matching delegated runs, which are spawned from the home directory.
/// If discovery fails, the current directory is left untouched.
/// </summary>
internal sealed class AnchoringHomeDirectoryProvider : HomeDirectoryProvider
{
    private readonly IHomeDirectoryProvider _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnchoringHomeDirectoryProvider"/> class.
    /// </summary>
    /// <param name="inner">The provider that performs the actual discovery.</param>
    public AnchoringHomeDirectoryProvider(IHomeDirectoryProvider inner)
    {
        Guard.IsNotNull(inner);
        _inner = inner;
    }

    /// <inheritdoc />
    protected override string Resolve()
    {
        var homeDirectory = _inner.HomeDirectory;
        Directory.SetCurrentDirectory(homeDirectory);
        return homeDirectory;
    }
}
