// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.ServerAdapters.Internal.GitHub;

/// <summary>
/// The members <c>bv</c> sends to edit a GitHub release.
/// </summary>
internal sealed record GitHubReleaseUpdate
{
    /// <summary>Gets the name of the tag of the release.</summary>
    public required string TagName { get; init; }

    /// <summary>Gets the name of the release.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the branch or commit SHA that the tag is created from, when the tag does not exist.</summary>
    public required string TargetCommitish { get; init; }

    /// <summary>Gets the description of the release, in Markdown.</summary>
    public required string Body { get; init; }

    /// <summary>Gets a value indicating whether the release is a prerelease.</summary>
    public required bool Prerelease { get; init; }

    /// <summary>Gets a value indicating whether the release is a draft.</summary>
    public required bool Draft { get; init; }
}
