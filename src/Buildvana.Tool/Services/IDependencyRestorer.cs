// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Buildvana.Tool.Services.Solution;

namespace Buildvana.Tool.Services;

/// <summary>
/// Restores the solution the way <c>bv dependencies</c> needs it restored.
/// </summary>
/// <remarks>
/// <para>This restore differs from the one a build asks for in three ways. It forces the restore, so that no
/// cached result stands in for the audit. It can leave the transitive override files out of the evaluation,
/// which is how the graph as it stands without them is obtained. And it reports the exit code instead of
/// failing on it, because a restore that reports audit findings as errors fails while writing everything the
/// caller reads.</para>
/// </remarks>
internal interface IDependencyRestorer
{
    /// <summary>
    /// Restores the solution.
    /// </summary>
    /// <param name="solution">The solution to restore.</param>
    /// <param name="suppressTransitiveOverrides"><see langword="true"/> to leave the transitive override
    /// files out of the evaluation, <see langword="false"/> to let the SDK import them.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the spawned
    /// <c>dotnet</c> child process.</param>
    /// <returns>A task whose result is the exit code of the <c>dotnet restore</c> invocation.</returns>
    Task<int> RestoreAsync(
        SolutionContext solution,
        bool suppressTransitiveOverrides,
        CancellationToken cancellationToken = default);
}
