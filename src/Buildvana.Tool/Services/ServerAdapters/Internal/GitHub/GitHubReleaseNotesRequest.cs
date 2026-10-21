// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.ServerAdapters.Internal.GitHub;

/// <summary>
/// The members <c>bv</c> sends to have GitHub generate the notes of a release.
/// </summary>
internal sealed record GitHubReleaseNotesRequest
{
    /// <summary>Gets the name of the tag of the release.</summary>
    public required string TagName { get; init; }

    /// <summary>Gets the branch or commit SHA that the tag is created from, when the tag does not exist.</summary>
    public required string TargetCommitish { get; init; }
}
