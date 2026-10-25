// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Hosts.Forgejo;

/// <summary>
/// The members <c>bv</c> sends to edit a Forgejo release.
/// </summary>
/// <remarks>
/// <para>Forgejo keeps the current value of a string member that a request sends empty.</para>
/// </remarks>
internal sealed record ForgejoReleaseUpdate
{
    /// <summary>
    /// Gets the name of the release.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the description of the release, in Markdown.
    /// </summary>
    public required string Body { get; init; }

    /// <summary>
    /// Gets a value indicating whether the release is a prerelease.
    /// </summary>
    public required bool Prerelease { get; init; }

    /// <summary>
    /// Gets a value indicating whether the release is a draft.
    /// </summary>
    public required bool Draft { get; init; }
}
