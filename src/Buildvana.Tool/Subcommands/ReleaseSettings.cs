// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using Buildvana.Core;
using Buildvana.Core.Versioning;
using Buildvana.Runtime;
using Buildvana.Tool.CommandLine;
using Buildvana.Tool.Services;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Subcommands;

/// <summary>
/// Options for the <c>release</c> command. The flag values are parsed from the command-line option tokens by
/// <see cref="Parse"/>; each <c>Resolve*</c> method then merges the flag with the <c>release</c> configuration
/// section and a hardcoded default (flag → config → default).
/// Decorated with <see cref="BvOptionAttribute"/>/<see cref="DescriptionAttribute"/> for the help renderer.
/// </summary>
internal sealed class ReleaseSettings
{
    private static readonly IReadOnlyList<string> DefaultGenerateDocsFrom = ["main", "master"];
    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromSeconds(1);

    private readonly ReleaseConfig? _config;
    private readonly DotNetSettings _dotNetSettings;

    private ReleaseSettings(ReleaseConfig? config, DotNetSettings dotNetSettings)
    {
        _config = config;
        _dotNetSettings = dotNetSettings;
    }

    /// <summary>
    /// Gets the MSBuild configuration to build.
    /// </summary>
    [BvOption("-c|--configuration <NAME>")]
    [Description("MSBuild configuration to build. Defaults to the configured value, or 'Release'.")]
    public string? Configuration { get; init; }

    /// <summary>
    /// Gets the requested version-spec change.
    /// </summary>
    [BvOption("--bump <CHANGE>")]
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
    [BvOption("--check-public-api <BOOL>")]
    [Description("Check the public API when computing version-spec changes. Defaults to true.")]
    public bool? CheckPublicApi { get; init; }

    /// <summary>
    /// Gets a value indicating whether in-tree references to packages produced by this release are updated.
    /// </summary>
    [BvOption("--dogfood <BOOL>")]
    [Description("Update in-tree references to packages produced by this release. Defaults to true.")]
    public bool? Dogfood { get; init; }

    /// <summary>
    /// Parses the command's option tokens into a <see cref="ReleaseSettings"/> and binds the configuration
    /// sources consulted by the <c>Resolve*</c> methods. Unknown options have already been rejected by
    /// <c>CommandArgumentValidator</c>, so every option token is one the command declares.
    /// </summary>
    /// <param name="options">The option tokens for the <c>release</c> command (from <c>CommandParameters.Options</c>).</param>
    /// <param name="config">The Buildvana configuration whose <c>release</c> section layers between the flags and the defaults.</param>
    /// <param name="dotNetSettings">The resolved <c>dotnet</c> settings, providing the fallback build configuration.</param>
    /// <returns>The parsed settings.</returns>
    /// <exception cref="BuildFailedException">An option value is invalid.</exception>
    public static ReleaseSettings Parse(
        IReadOnlyList<string> options,
        BuildvanaConfig config,
        DotNetSettings dotNetSettings)
    {
        Guard.IsNotNull(options);
        Guard.IsNotNull(config);
        Guard.IsNotNull(dotNetSettings);
        var reader = new CliOptionReader(options);
        return new ReleaseSettings(config.Release, dotNetSettings)
        {
            Configuration = reader.ReadValue("--configuration", "-c"),
            Bump = reader.ReadValue("--bump"),
            CheckPublicApi = reader.ReadBoolValue("--check-public-api"),
            Dogfood = reader.ReadBoolValue("--dogfood"),
        };
    }

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
    /// Gets the resolved MSBuild configuration: <see cref="Configuration"/> (the <c>--configuration</c> flag) if set,
    /// otherwise <c>release.configuration</c>, otherwise the configured <c>dotnet</c> default.
    /// </summary>
    public string ResolveConfiguration() => Configuration ?? _config?.Configuration ?? _dotNetSettings.Configuration;

    /// <summary>
    /// Returns <see cref="CheckPublicApi"/> if set, otherwise <c>release.checkPublicApi</c>, otherwise <see langword="true"/>.
    /// </summary>
    public bool ResolveCheckPublicApi() => CheckPublicApi ?? _config?.CheckPublicApi ?? true;

    /// <summary>
    /// Returns the configured changelog-update policy (<c>release.changelogUpdates</c>), or
    /// <see cref="ChangelogUpdates.Stable"/> when unset.
    /// </summary>
    public ChangelogUpdates ResolveChangelogUpdates() => _config?.ChangelogUpdates ?? ChangelogUpdates.Stable;

    /// <summary>
    /// Returns the text substituted for an empty changelog (<c>release.emptyChangelog</c>), or <see langword="null"/>
    /// when unset or all whitespace (in which case an empty changelog fails the release).
    /// </summary>
    /// <remarks>
    /// <para>Text that is all whitespace would substitute nothing for nothing, so it counts as no substitute at all:
    /// the release fails with the same actionable message as when <c>release.emptyChangelog</c> is missing, instead
    /// of silently producing a release section with no body.</para>
    /// </remarks>
    public string? ResolveEmptyChangelog()
    {
        var configured = _config?.EmptyChangelog;
        return string.IsNullOrWhiteSpace(configured) ? null : configured;
    }

    /// <summary>
    /// Returns <see cref="Dogfood"/> if set, otherwise <c>release.dogfood</c>, otherwise <see langword="true"/>.
    /// </summary>
    public bool ResolveDogfood() => Dogfood ?? _config?.Dogfood ?? true;

    /// <summary>
    /// Determines whether documentation is generated when releasing from <paramref name="branch"/>, by matching it
    /// against the configured <c>release.generateDocsFrom</c> regular expressions (default <c>main</c>/<c>master</c>).
    /// </summary>
    /// <param name="branch">The short name of the branch the release is created from.</param>
    /// <returns><see langword="true"/> if <paramref name="branch"/> matches at least one pattern;
    /// otherwise, <see langword="false"/>.</returns>
    /// <exception cref="BuildFailedException">A configured pattern is not a valid regular expression, or matching timed out.</exception>
    /// <remarks>
    /// Patterns are implicitly anchored (wrapped in <c>^(?:</c>&#8230;<c>)$</c>): a pattern must match the whole
    /// branch name, so <c>main</c> matches neither <c>domain</c> nor <c>main2</c>.
    /// </remarks>
    public bool MatchesDocsBranch(string branch)
    {
        Guard.IsNotNull(branch);
        if (branch.Length == 0)
        {
            return false;
        }

        var patterns = _config?.GenerateDocsFrom ?? DefaultGenerateDocsFrom;
        return patterns.Any(pattern => IsMatch(pattern, branch));
    }

    private static bool IsMatch(string pattern, string branch)
    {
        try
        {
            return Regex.IsMatch(branch, $"^(?:{pattern})$", RegexOptions.CultureInvariant, RegexMatchTimeout);
        }
        catch (ArgumentException ex)
        {
            throw new BuildFailedException($"Invalid regular expression '{pattern}' in release.generateDocsFrom: {ex.Message}");
        }
        catch (RegexMatchTimeoutException)
        {
            throw new BuildFailedException(
                $"Regular expression '{pattern}' in release.generateDocsFrom timed out matching branch '{branch}'.");
        }
    }
}
