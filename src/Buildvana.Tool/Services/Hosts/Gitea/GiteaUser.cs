// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Hosts.Gitea;

/// <summary>
/// The members of a Gitea user or organization that <c>bv</c> reads.
/// </summary>
internal sealed record GiteaUser
{
    /// <summary>
    /// Gets the visibility of the user or organization: <c>public</c>, <c>limited</c>, or <c>private</c>.
    /// </summary>
    public required string Visibility { get; init; }
}
