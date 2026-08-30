// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using Buildvana.Core.Json.Schema;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Models the <c>git.identity</c> section of a Buildvana configuration file.
/// </summary>
/// <remarks>
/// <para><c>required</c> puts both members in the schema's <c>required</c> list: an identity that is stated at
/// all must state them. See <see cref="BuildvanaJsonConfig"/> for why a required member is not nullable.</para>
/// </remarks>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record GitIdentityJsonConfig
{
    /// <summary>Gets the display name of the Git identity.</summary>
    [JsonSchemaExample("\"My Bot\"")]
    [Description("Display name used as the Git author/committer.")]
    public required string Name { get; init; }

    /// <summary>Gets the email address of the Git identity.</summary>
    [JsonSchemaExample("\"bot@example.invalid\"")]
    [Description("Email address used as the Git author/committer.")]
    public required string Email { get; init; }
}
