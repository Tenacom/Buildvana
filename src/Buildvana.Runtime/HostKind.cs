// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Buildvana.Runtime;

/// <summary>
/// Specifies the kind of Git host that the releases of a repository go to.
/// </summary>
public enum HostKind
{
    /// <summary>The kind of host is detected from the repository URL and from the CI platform.</summary>
    [JsonStringEnumMemberName("auto")]
    Auto,

    /// <summary>The host is GitHub: <c>github.com</c>, or a GitHub Enterprise Server.</summary>
    [JsonStringEnumMemberName("github")]
    GitHub,

    /// <summary>The host is GitLab: <c>gitlab.com</c>, or a self-managed GitLab instance.</summary>
    [JsonStringEnumMemberName("gitlab")]
    GitLab,
}
