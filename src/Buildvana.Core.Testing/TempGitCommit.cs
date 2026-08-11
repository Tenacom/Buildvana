// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Buildvana.Core.Testing;

/// <summary>
/// Describes a commit of a <see cref="TempGitRepo"/>, as returned by <see cref="TempGitRepo.GetCommits"/>.
/// </summary>
/// <param name="Sha">The SHA of the commit.</param>
/// <param name="Message">The commit message, stripped of trailing newlines.</param>
/// <param name="CommitterName">The name of the commit's committer.</param>
/// <param name="CommitterEmail">The email address of the commit's committer.</param>
/// <param name="ChangedFiles">The paths, relative to the repository's root and in ordinal order, of the files
/// the commit changed with respect to its first parent. Empty for an empty commit.</param>
public sealed record TempGitCommit(
    string Sha,
    string Message,
    string CommitterName,
    string CommitterEmail,
    IReadOnlyList<string> ChangedFiles);
