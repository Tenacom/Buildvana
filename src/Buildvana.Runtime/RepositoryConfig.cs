// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// The resolved configuration of the repository that the releases go to.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record RepositoryConfig
{
    /// <summary>
    /// Gets the URL of the repository that the releases go to, or <see langword="null"/> when the configuration
    /// does not state one, and <c>bv release</c> cannot run.
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1056:URI-like properties should not be strings",
        Justification = "Git accepts the scp-like syntax, such as git@host:owner/repo, which System.Uri cannot hold.")]
    public string? Url { get; init; }

    /// <summary>Gets the kind of Git host of the repository.</summary>
    public HostKind Host { get; init; } = HostKind.Auto;
}
