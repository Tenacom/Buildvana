// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Hosts.GitLab;

/// <summary>
/// The members of a GitLab user that <c>bv</c> reads.
/// </summary>
internal sealed record GitLabUser
{
    /// <summary>Gets the identifier of the user.</summary>
    public required long Id { get; init; }
}
