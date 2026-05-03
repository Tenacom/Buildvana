// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Core.HomeDirectory;

/// <summary>
/// Recommended base class for implementations of <see cref="IHomeDirectoryProvider"/>.
/// Defers the call to <see cref="Resolve"/> to first read of <see cref="HomeDirectory"/>
/// and caches the result (and any exception) for the lifetime of the instance.
/// </summary>
public abstract class HomeDirectoryProvider : IHomeDirectoryProvider
{
    private readonly Lazy<string> _lazy;

    /// <summary>
    /// Initializes a new instance of the <see cref="HomeDirectoryProvider"/> class.
    /// </summary>
    protected HomeDirectoryProvider()
    {
        _lazy = new Lazy<string>(Resolve);
    }

    /// <inheritdoc />
    public string HomeDirectory => _lazy.Value;

    /// <summary>
    /// When overridden in a derived class, computes the absolute path of the home directory.
    /// Invoked at most once per instance, on first read of <see cref="HomeDirectory"/>.
    /// </summary>
    /// <remarks>
    /// <para>If the home directory cannot be resolved, the implementation should throw a
    /// <see cref="BuildFailedException"/>.</para>
    /// </remarks>
    /// <returns>The absolute path of the home directory, with a trailing directory separator.</returns>
    protected abstract string Resolve();
}
