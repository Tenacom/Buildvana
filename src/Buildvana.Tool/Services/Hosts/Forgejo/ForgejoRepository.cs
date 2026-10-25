// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Hosts.Forgejo;

/// <summary>
/// The members of a Forgejo repository that <c>bv</c> reads.
/// </summary>
internal sealed record ForgejoRepository
{
    /// <summary>
    /// Gets the owner of the repository.
    /// </summary>
    public required ForgejoUser Owner { get; init; }

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
    public required ForgejoPermission Permissions { get; init; }
}
