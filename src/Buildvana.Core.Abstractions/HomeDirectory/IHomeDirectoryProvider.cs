// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.HomeDirectory;

/// <summary>
/// Provides the absolute path of the directory considered "home" by the current build —
/// the discovered repository root for tools, or a value supplied by an MSBuild task.
/// </summary>
/// <remarks>
/// <para>Implementations are required to defer any work that may fail (such as filesystem discovery)
/// until <see cref="HomeDirectory"/> is read for the first time, and to cache the result thereafter.
/// Use <see cref="HomeDirectoryProvider"/> as a base class to obtain those semantics for free.</para>
/// </remarks>
public interface IHomeDirectoryProvider
{
    /// <summary>
    /// Gets the absolute path of the home directory, with a trailing directory separator.
    /// </summary>
    /// <remarks>
    /// <para>If the home directory cannot be resolved, the build is failed via <see cref="IBuildHost.Fail"/>;
    /// the exact exception type thrown is host-specific.</para>
    /// </remarks>
    string HomeDirectory { get; }
}
