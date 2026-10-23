// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Hosts.GitLab;

/// <summary>
/// The members of a GitLab release that <c>bv</c> reads.
/// </summary>
internal sealed record GitLabRelease
{
    /// <summary>Gets the name of the tag of the release, which identifies the release in the API.</summary>
    public required string TagName { get; init; }
}
