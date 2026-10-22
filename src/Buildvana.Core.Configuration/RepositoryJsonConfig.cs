// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Buildvana.Core.Json.Schema;
using Buildvana.Runtime;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Models the <c>repository</c> section of a Buildvana configuration file.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record RepositoryJsonConfig
{
    /// <summary>Gets the URL of the repository that the releases go to.</summary>
    [JsonSchemaExample("\"https://github.com/Tenacom/Buildvana\"")]
    [Description("URL of the repository that releases go to. Required by bv release.")]
    [SuppressMessage(
        "Design",
        "CA1056:URI-like properties should not be strings",
        Justification = "Git accepts the scp-like syntax, such as git@host:owner/repo, which System.Uri cannot hold.")]
    public string? Url { get; init; }

    /// <summary>Gets the kind of Git host of the repository.</summary>
    [Description("Kind of Git host of the repository.")]
    public HostKind? Host { get; init; }
}
