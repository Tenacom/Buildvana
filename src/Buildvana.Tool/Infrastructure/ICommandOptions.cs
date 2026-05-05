// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Infrastructure;

/// <summary>
/// Provides access to command-line option values, by name.
/// </summary>
/// <remarks>
/// This is a placeholder abstraction. The current implementation is empty (see <see cref="EmptyCommandOptions"/>);
/// a real command-line argument parser will be wired in when bv grows its own CLI surface.
/// </remarks>
public interface ICommandOptions
{
    /// <summary>
    /// Gets the value of the named command-line option.
    /// </summary>
    /// <param name="name">The option name.</param>
    /// <returns>The option value, or <see langword="null"/> if no such option was provided.</returns>
    string? GetValue(string name);
}
