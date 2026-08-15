// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// The resolved <c>bv release</c> workflow configuration.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ReleaseConfig
{
    /// <summary>
    /// Gets the regular expressions (implicitly anchored, matched against the whole short branch name)
    /// identifying branches that produce public releases. When empty, no branch produces a public release.
    /// </summary>
    public IReadOnlyList<string> Branches { get; init; } = [];

    /// <summary>Gets the build configuration used to produce release artifacts.</summary>
    /// <remarks>
    /// <para>When neither the command line nor the configuration file states a release-specific
    /// configuration, this value equals <see cref="DotNetConfig.Configuration"/>.</para>
    /// </remarks>
    public string Configuration { get; init; } = DotNetConfig.DefaultConfiguration;

    /// <summary>Gets a value indicating whether public API files are checked before a release.</summary>
    public bool CheckPublicApi { get; init; } = true;

    /// <summary>Gets the policy specifying which releases require a changelog update.</summary>
    public ChangelogUpdates ChangelogUpdates { get; init; } = ChangelogUpdates.Stable;

    /// <summary>
    /// Gets the text substituted when a release has no changelog entries, or <see langword="null"/> when an
    /// empty changelog must fail the release.
    /// </summary>
    public string? EmptyChangelog { get; init; }

    /// <summary>Gets a value indicating whether self-references are updated (dogfooding) during a release.</summary>
    public bool Dogfood { get; init; } = true;
}
