// Copyright (C) Tenacom and contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using JetBrains.Annotations;

namespace Buildvana.Tool.Services.Hosts.GitLab;

/// <summary>
/// The assets <c>bv</c> sends with a new GitLab release.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
internal sealed record GitLabReleaseAssets
{
    /// <summary>Gets the links of the release.</summary>
    public required IReadOnlyList<GitLabReleaseLink> Links { get; init; }
}
