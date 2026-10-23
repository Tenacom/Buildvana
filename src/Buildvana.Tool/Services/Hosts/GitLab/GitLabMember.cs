// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Hosts.GitLab;

/// <summary>
/// The members of a GitLab project member that <c>bv</c> reads.
/// </summary>
internal sealed record GitLabMember
{
    /// <summary>Gets the access level of the member, such as 30 for Developer and 40 for Maintainer.</summary>
    public required int AccessLevel { get; init; }
}
