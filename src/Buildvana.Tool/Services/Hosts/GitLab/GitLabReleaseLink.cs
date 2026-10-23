// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Hosts.GitLab;

/// <summary>
/// The members <c>bv</c> sends for a link of a new GitLab release.
/// </summary>
internal sealed record GitLabReleaseLink
{
    /// <summary>Gets the name of the link, as the release page shows it.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the URL the link points at.</summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets the path of the permanent link of the asset, relative to
    /// <c>https://&lt;host&gt;/&lt;project&gt;/-/releases/&lt;tag&gt;/downloads</c>, such as <c>/Foo.1.0.0.nupkg</c>.
    /// </summary>
    public required string DirectAssetPath { get; init; }

    /// <summary>Gets the type of the link: <c>other</c>, <c>runbook</c>, <c>image</c>, or <c>package</c>.</summary>
    public required string LinkType { get; init; }
}
