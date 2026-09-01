// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Services;
using Buildvana.Tool.Services.Solution;

/// <summary>
/// A restorer that runs a test's own script instead of <c>dotnet restore</c>, so that a pass of the override
/// lifecycle sees the graph the test decided it would.
/// </summary>
/// <remarks>
/// <para>The callback stands where the child process would: it writes the assets files the next read finds,
/// and its answer is the exit code the restore reports.</para>
/// </remarks>
internal sealed class FakeDependencyRestorer : IDependencyRestorer
{
    /// <summary>
    /// Gets or sets what a restore does. Its argument says whether the override files are suppressed, and
    /// its result is the exit code.
    /// </summary>
    public Func<bool, int> OnRestore { get; set; } = static _ => 0;

    /// <summary>Gets one entry per restore, stating whether the override files were suppressed.</summary>
    public List<bool> Restores { get; } = [];

    /// <inheritdoc/>
    public Task<int> RestoreAsync(
        SolutionContext solution,
        bool suppressTransitiveOverrides,
        CancellationToken cancellationToken = default)
    {
        Restores.Add(suppressTransitiveOverrides);
        return Task.FromResult(OnRestore(suppressTransitiveOverrides));
    }
}
