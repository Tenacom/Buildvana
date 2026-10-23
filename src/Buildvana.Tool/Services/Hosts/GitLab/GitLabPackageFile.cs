// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Hosts.GitLab;

/// <summary>
/// The members of a GitLab package file that <c>bv</c> reads.
/// </summary>
internal sealed record GitLabPackageFile
{
    /// <summary>Gets the identifier of the package the file belongs to.</summary>
    public required long PackageId { get; init; }
}
