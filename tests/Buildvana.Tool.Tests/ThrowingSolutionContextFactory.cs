// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Tool.Services.Solution;

/// <summary>
/// A solution context factory that refuses to answer, recording that it was asked.
/// </summary>
/// <remarks>
/// <para>Reading the solution is the one step of <c>bv dependencies</c> that spawns MSBuild, and only the
/// packages scope asks for it. A run that leaves that scope out and reaches for the solution anyway fails
/// here, rather than passing at the price of a build nobody wanted.</para>
/// </remarks>
internal sealed class ThrowingSolutionContextFactory : ISolutionContextFactory
{
    /// <summary>
    /// Gets a value indicating whether the factory was asked for a solution context.
    /// </summary>
    public bool WasAsked { get; private set; }

    /// <inheritdoc/>
    public SolutionContext Create()
    {
        WasAsked = true;
        throw new BuildFailedException("This run was not supposed to read the solution.");
    }
}
