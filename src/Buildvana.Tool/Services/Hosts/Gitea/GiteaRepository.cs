// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Hosts.Gitea;

/// <summary>
/// The members of a Gitea repository that <c>bv</c> reads.
/// </summary>
internal sealed record GiteaRepository
{
    /// <summary>
    /// Gets the owner of the repository.
    /// </summary>
    public required GiteaUser Owner { get; init; }

    /// <summary>
    /// Gets a value indicating whether the repository is private.
    /// </summary>
    public required bool Private { get; init; }

    /// <summary>
    /// Gets a value indicating whether the releases unit of the repository is enabled.
    /// </summary>
    public required bool HasReleases { get; init; }

    /// <summary>
    /// Gets the permissions of the authenticated user on the repository.
    /// </summary>
    public required GiteaPermission Permissions { get; init; }
}
