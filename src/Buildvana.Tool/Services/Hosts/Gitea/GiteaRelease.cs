// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Hosts.Gitea;

/// <summary>
/// The members of a Gitea release that <c>bv</c> reads.
/// </summary>
internal sealed record GiteaRelease
{
    /// <summary>
    /// Gets the identifier of the release.
    /// </summary>
    public required long Id { get; init; }
}
