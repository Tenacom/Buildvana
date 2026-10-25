// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Hosts.Gitea;

/// <summary>
/// The members of the permissions of a user on a Gitea repository that <c>bv</c> reads.
/// </summary>
internal sealed record GiteaPermission
{
    /// <summary>
    /// Gets a value indicating whether the user can push to the repository.
    /// </summary>
    public required bool Push { get; init; }
}
