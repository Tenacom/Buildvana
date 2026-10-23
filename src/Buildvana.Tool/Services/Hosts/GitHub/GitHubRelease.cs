// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Hosts.GitHub;

/// <summary>
/// The members of a GitHub release that <c>bv</c> reads.
/// </summary>
internal sealed record GitHubRelease
{
    /// <summary>Gets the identifier of the release.</summary>
    public required long Id { get; init; }

    /// <summary>
    /// Gets the URI template that asset uploads go to, such as
    /// <c>https://uploads.github.com/repos/o/r/releases/1/assets{?name,label}</c>.
    /// </summary>
    public required string UploadUrl { get; init; }
}
