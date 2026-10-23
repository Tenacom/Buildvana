// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Hosts.GitHub;

/// <summary>
/// The members of a GitHub repository that <c>bv</c> reads.
/// </summary>
internal sealed record GitHubRepository
{
    /// <summary>Gets a value indicating whether the repository is private.</summary>
    public required bool Private { get; init; }
}
