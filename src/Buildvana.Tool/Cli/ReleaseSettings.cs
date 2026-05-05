// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Buildvana.Tool.Cli;

/// <summary>
/// Options for the <c>release</c> command.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class ReleaseSettings : BuildSettings
{
    /// <summary>
    /// Gets the requested version-spec change.
    /// </summary>
    [CommandOption("--versionSpecChange|--version-spec-change <CHANGE>")]
    [Description("Version-spec change to apply. Defaults to None.")]
    public string? VersionSpecChange { get; init; }

    /// <summary>
    /// Gets a value indicating whether public API files are checked when computing version-spec changes.
    /// </summary>
    [CommandOption("--checkPublicApiFiles|--check-public-api-files <BOOL>")]
    [Description("Check public API files when computing version-spec changes. Defaults to true.")]
    public bool? CheckPublicApiFiles { get; init; }

    /// <summary>
    /// Gets a value indicating whether the changelog is updated on prereleases.
    /// </summary>
    [CommandOption("--updateChangelogOnPrerelease|--update-changelog-on-prerelease <BOOL>")]
    [Description("Update the changelog on prereleases. Defaults to false.")]
    public bool? UpdateChangelogOnPrerelease { get; init; }

    /// <summary>
    /// Gets a value indicating whether the build is failed if the 'Unreleased changes' section is empty.
    /// </summary>
    [CommandOption("--ensureChangelogNotEmpty|--ensure-changelog-not-empty <BOOL>")]
    [Description("Fail the build if the 'Unreleased changes' section is empty. Defaults to true.")]
    public bool? EnsureChangelogNotEmpty { get; init; }

    /// <summary>
    /// Gets a value indicating whether in-tree references to packages produced by this release are updated.
    /// </summary>
    [CommandOption("--updateSelfReferences|--update-self-references <BOOL>")]
    [Description("Update in-tree references to packages produced by this release (dogfooding). Defaults to true.")]
    public bool? UpdateSelfReferences { get; init; }
}
