// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.Versioning;

/// <summary>
/// Represents the result of a <see cref="GitHeightCalculator"/> run: the Git height of the version line
/// being built, plus the repository facts needed to compute version strings.
/// </summary>
/// <param name="Height">The Git height of the version line being built. 0 means the line has no committed
/// history: the version file is not committed, its committed <c>MAJOR.MINOR</c> differs from the one being
/// built, or the repository has no commits.</param>
/// <param name="BranchName">The short name of the current branch, or the empty string if HEAD is detached
/// or the current branch has no commits.</param>
/// <param name="CommitId">The SHA of the current HEAD commit, or <see langword="null"/> if the repository
/// has no commits.</param>
public sealed record GitHeightResult(int Height, string BranchName, string? CommitId);
