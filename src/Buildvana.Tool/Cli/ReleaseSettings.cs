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
    [CommandOption("--bump <CHANGE>")]
    [Description("""
        Version-spec change to apply:
          - [bold]none[/] (the default): advance patch from Git height.
          - [bold]unstable[/]: advance patch, add prerelease label.
          - [bold]stable[/]: advance patch, drop prerelease label.
          - [bold]minor[/]: advance minor, reset patch, add prerelease label.
          - [bold]major[/]: advance major, reset minor and patch, add prerelease label.
        """)]
    public string? Bump { get; init; }

    /// <summary>
    /// Gets a value indicating whether the public API is checked when computing version-spec changes.
    /// </summary>
    [CommandOption("--check-public-api <BOOL>")]
    [Description("Check the public API when computing version-spec changes. Defaults to true.")]
    public bool? CheckPublicApi { get; init; }

    /// <summary>
    /// Gets a value indicating whether the changelog is updated on unstable (prerelease) versions.
    /// </summary>
    [CommandOption("--unstable-changelog <BOOL>")]
    [Description("Update the changelog on unstable (prerelease) versions. Defaults to false.")]
    public bool? UnstableChangelog { get; init; }

    /// <summary>
    /// Gets a value indicating whether the build is failed if the 'Unreleased changes' section is empty.
    /// </summary>
    [CommandOption("--require-changelog <BOOL>")]
    [Description("Fail the build if the 'Unreleased changes' section is empty. Defaults to true.")]
    public bool? RequireChangelog { get; init; }

    /// <summary>
    /// Gets a value indicating whether in-tree references to packages produced by this release are updated.
    /// </summary>
    [CommandOption("--dogfood <BOOL>")]
    [Description("Update in-tree references to packages produced by this release. Defaults to true.")]
    public bool? Dogfood { get; init; }
}
