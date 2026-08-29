// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Models a single NuGet push feed, as stated in a Buildvana configuration file.
/// </summary>
/// <remarks>
/// <para><c>required</c> puts both members in the schema's <c>required</c> list: a feed that is stated at all
/// must state them. See <see cref="BuildvanaJsonConfig"/> for why a required member is not nullable.</para>
/// </remarks>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NuGetFeedJsonConfig
{
    /// <summary>Gets the source URL of the NuGet feed.</summary>
    [Description("Source URL of the NuGet feed.")]
    public required string Source { get; init; }

    /// <summary>Gets the name of the environment variable holding the feed API key.</summary>
    [Description("Name of the environment variable that holds the feed API key.")]
    public required string ApiKeyEnv { get; init; }
}
