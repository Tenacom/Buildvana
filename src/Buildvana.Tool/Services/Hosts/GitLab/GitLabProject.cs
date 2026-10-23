// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Hosts.GitLab;

/// <summary>
/// The members of a GitLab project that <c>bv</c> reads.
/// </summary>
internal sealed record GitLabProject
{
    /// <summary>Gets the visibility of the project: <c>public</c>, <c>internal</c>, or <c>private</c>.</summary>
    public required string Visibility { get; init; }
}
