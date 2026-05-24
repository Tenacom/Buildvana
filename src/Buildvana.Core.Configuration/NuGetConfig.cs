// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Configures NuGet package publishing.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NuGetConfig
{
    /// <summary>Gets the push feeds, keyed by channel name (<c>prerelease</c>, <c>release</c>).</summary>
    [Description("Push feeds, keyed by channel name (prerelease, release).")]
    public IReadOnlyDictionary<string, NuGetFeedConfig>? Feeds { get; init; }
}
