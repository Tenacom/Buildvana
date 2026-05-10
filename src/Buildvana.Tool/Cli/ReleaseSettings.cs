// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using Buildvana.Core;
using Buildvana.Tool.Services.Versioning;
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

    /// <summary>
    /// Parses <see cref="Bump"/> into a <see cref="VersionSpecChange"/>; defaults to <see cref="VersionSpecChange.None"/>.
    /// </summary>
    /// <exception cref="BuildFailedException">The value of <see cref="Bump"/> is not a recognized version-spec change.</exception>
    public VersionSpecChange ResolveBump()
    {
        if (Bump is null)
        {
            return VersionSpecChange.None;
        }

        var parsed = Enum.TryParse<VersionSpecChange>(Bump, ignoreCase: true, out var value) && Enum.IsDefined(value);
        return parsed
            ? value
            : throw new BuildFailedException($"Invalid value '{Bump}' for --bump. Valid values: none, unstable, stable, minor, major.");
    }

    /// <summary>
    /// Returns <see cref="CheckPublicApi"/> if set, otherwise <see langword="true"/>.
    /// </summary>
    public bool ResolveCheckPublicApi() => CheckPublicApi ?? true;

    /// <summary>
    /// Returns <see cref="UnstableChangelog"/> if set, otherwise <see langword="false"/>.
    /// </summary>
    public bool ResolveUnstableChangelog() => UnstableChangelog ?? false;

    /// <summary>
    /// Returns <see cref="RequireChangelog"/> if set, otherwise <see langword="true"/>.
    /// </summary>
    public bool ResolveRequireChangelog() => RequireChangelog ?? true;

    /// <summary>
    /// Returns <see cref="Dogfood"/> if set, otherwise <see langword="true"/>.
    /// </summary>
    public bool ResolveDogfood() => Dogfood ?? true;
}
