// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Hosts.GitHub;

/// <summary>
/// The members of the release notes GitHub generates that <c>bv</c> reads.
/// </summary>
internal sealed record GitHubReleaseNotes
{
    /// <summary>Gets the generated notes, in Markdown.</summary>
    public required string Body { get; init; }
}
