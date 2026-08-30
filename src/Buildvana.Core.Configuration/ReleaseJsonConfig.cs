// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel;
using Buildvana.Core.Json.Schema;
using Buildvana.Runtime;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Models the <c>release</c> section of a Buildvana configuration file.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ReleaseJsonConfig
{
    /// <summary>Gets the regular expressions identifying branches that produce public releases.</summary>
    [JsonSchemaExample("""["^main$", "^v\\d+\\.\\d+$"]""")]
    [Description("Anchored regular expressions matching public-release branches.")]
    public IReadOnlyList<string>? Branches { get; init; }

    /// <summary>Gets the build configuration used to produce release artifacts.</summary>
    [JsonSchemaExample("\"Release\"")]
    [Description("Build configuration used to produce release artifacts.")]
    [JsonSchemaNoDefault] // The default is dynamic — the resolved dotnet.configuration — which no static value can state.
    public string? Configuration { get; init; }

    /// <summary>Gets a value indicating whether public API files are checked before a release.</summary>
    [Description("Whether public API files are checked before a release.")]
    public bool? CheckPublicApi { get; init; }

    /// <summary>Gets the policy specifying which releases require a changelog update.</summary>
    [Description("Which releases require a changelog update.")]
    public ChangelogUpdates? ChangelogUpdates { get; init; }

    /// <summary>Gets the text substituted when a release has no changelog entries.</summary>
    [JsonSchemaExample("\"This release contains no user-visible changes.\"")]
    [Description("Text substituted when a release has no changelog entries.")]
    public string? EmptyChangelog { get; init; }

    /// <summary>Gets a value indicating whether self-references are updated (dogfooding) during a release.</summary>
    [Description("Whether self-references are updated (dogfooding) during a release.")]
    public bool? Dogfood { get; init; }
}
