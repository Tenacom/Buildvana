// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services;

/// <summary>
/// The outcome of <see cref="SelfVersionService.UpdateRepositoryAsync"/>: the exit code of the run, and the
/// summary when this bv updated the files itself.
/// </summary>
/// <remarks>
/// <para>When the target version is not the running bv's own, the update is handed off to the target version
/// after the tool manifest step (see <see cref="SelfUpdateHandoff"/>). The handed-off run prints its own
/// summary, so this one has none, and its exit code is the child's.</para>
/// </remarks>
/// <param name="ExitCode">The exit code of the run: 0 for an update this bv performed, or the exit code of the
/// handed-off run.</param>
/// <param name="Summary">The per-target summary of what changed, or <see langword="null"/> when the update was
/// handed off.</param>
internal sealed record SelfUpdateResult(int ExitCode, SelfUpdateSummary? Summary)
{
    /// <summary>
    /// Creates the result of an update this bv performed itself.
    /// </summary>
    /// <param name="summary">The per-target summary of what changed.</param>
    /// <returns>The result.</returns>
    public static SelfUpdateResult InPlace(SelfUpdateSummary summary) => new(0, summary);

    /// <summary>
    /// Creates the result of an update handed off to the target version.
    /// </summary>
    /// <param name="exitCode">The exit code of the handed-off run.</param>
    /// <returns>The result.</returns>
    public static SelfUpdateResult HandedOff(int exitCode) => new(exitCode, null);
}
