// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Configures the <c>bv release</c> workflow.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ReleaseConfig
{
    /// <summary>Gets the regular expressions identifying branches that produce public releases.</summary>
    [Description("Regular expressions (matched against the short branch name) identifying branches that produce public releases.")]
    public IReadOnlyList<string>? Branches { get; init; }

    /// <summary>Gets the regular expressions identifying branches that documentation is generated from.</summary>
    [Description("Regular expressions (matched against the short branch name) identifying branches that documentation is generated from.")]
    public IReadOnlyList<string>? GenerateDocsFrom { get; init; }

    /// <summary>Gets the build configuration used to produce release artifacts.</summary>
    [Description("Build configuration used to produce release artifacts. Defaults to dotnet.configuration when omitted.")]
    public string? Configuration { get; init; }

    /// <summary>Gets a value indicating whether public API files are checked before a release.</summary>
    [Description("Whether public API files are checked before a release.")]
    public bool? CheckPublicApi { get; init; }

    /// <summary>Gets the policy specifying which releases require a changelog update.</summary>
    [Description("Which releases require a changelog update.")]
    public ChangelogUpdates? ChangelogUpdates { get; init; }

    /// <summary>Gets the text substituted when a release has no changelog entries.</summary>
    [Description("Text substituted when a release has no changelog entries. When omitted, an empty changelog fails the release.")]
    public string? EmptyChangelog { get; init; }

    /// <summary>Gets a value indicating whether self-references are updated (dogfooding) during a release.</summary>
    [Description("Whether self-references are updated (dogfooding) during a release.")]
    public bool? Dogfood { get; init; }

    /// <summary>Gets the prerelease tag applied to prerelease versions.</summary>
    [Description("Prerelease tag applied to prerelease versions. When omitted, prerelease versions are not allowed.")]
    public string? PrereleaseTag { get; init; }
}
