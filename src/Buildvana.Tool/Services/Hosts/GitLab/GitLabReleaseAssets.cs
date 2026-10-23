// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Buildvana.Tool.Services.Hosts.GitLab;

/// <summary>
/// The assets <c>bv</c> sends with a new GitLab release.
/// </summary>
internal sealed record GitLabReleaseAssets
{
    /// <summary>Gets the links of the release.</summary>
    public required IReadOnlyList<GitLabReleaseLink> Links { get; init; }
}
