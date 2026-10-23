// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Hosts.GitLab;

/// <summary>
/// The members <c>bv</c> sends to create a GitLab release.
/// </summary>
internal sealed record GitLabNewRelease
{
    /// <summary>Gets the name of the tag of the release. The tag must exist.</summary>
    public required string TagName { get; init; }

    /// <summary>Gets the name of the release.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the description of the release, in Markdown.</summary>
    public required string Description { get; init; }

    /// <summary>Gets the assets of the release.</summary>
    public required GitLabReleaseAssets Assets { get; init; }
}
