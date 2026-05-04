// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Solution;

/// <summary>
/// Discovers and parses the solution that the current build operates on.
/// </summary>
public interface ISolutionContextFactory
{
    /// <summary>
    /// Discovers the solution file, parses it, and returns a <see cref="SolutionContext"/> describing it.
    /// </summary>
    /// <returns>A new <see cref="SolutionContext"/> instance.</returns>
    /// <exception cref="Buildvana.Core.BuildFailedException">No solution file was found, or the solution file could not be parsed.</exception>
    SolutionContext Create();
}
