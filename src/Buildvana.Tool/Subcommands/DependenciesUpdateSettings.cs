// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel;
using Buildvana.Core;
using Buildvana.Tool.CommandLine;
using Buildvana.Tool.Services.Dependencies;
using CommunityToolkit.Diagnostics;
using NuGet.Versioning;

namespace Buildvana.Tool.Subcommands;

/// <summary>
/// Options for the <c>dependencies update</c> command, parsed from the command's option tokens by
/// <see cref="Parse"/>.
/// </summary>
/// <remarks>
/// <para>The scope-selecting options are those of <c>dependencies show</c>, declared again because the help
/// renderer and the argument validator read what a settings type declares itself. What they mean is
/// <see cref="DependencyScopeSelection"/>'s business.</para>
/// </remarks>
internal sealed class DependenciesUpdateSettings
{
    /// <summary>Gets the package ids, or globs, naming the pins the invocation is about.</summary>
    [BvArgument("[ID...]")]
    [Description("Package ids, or globs, naming the pins to manage. Every pin, when none is given.")]
    public IReadOnlyList<string> Filters { get; init; } = [];

    /// <summary>Gets a value indicating whether the command line names the .NET SDK scope.</summary>
    [BvOption("--netsdk")]
    [Description("Manage the .NET SDK version pinned in global.json.")]
    public bool NetSdk { get; init; }

    /// <summary>Gets a value indicating whether the command line names the project SDK scope.</summary>
    [BvOption("--sdks")]
    [Description("Manage the MSBuild project SDKs.")]
    public bool Sdks { get; init; }

    /// <summary>Gets a value indicating whether the command line names the tools scope.</summary>
    [BvOption("--tools")]
    [Description("Manage the .NET local tools.")]
    public bool Tools { get; init; }

    /// <summary>Gets a value indicating whether the command line names the packages scope.</summary>
    [BvOption("--packages")]
    [Description("Manage the NuGet package pins.")]
    public bool Packages { get; init; }

    /// <summary>Gets a value indicating whether the command line leaves out the .NET SDK scope.</summary>
    [BvOption("--no-netsdk")]
    [Description("Leave the .NET SDK version alone.")]
    public bool NoNetSdk { get; init; }

    /// <summary>Gets a value indicating whether the command line leaves out the project SDK scope.</summary>
    [BvOption("--no-sdks")]
    [Description("Leave the MSBuild project SDKs alone.")]
    public bool NoSdks { get; init; }

    /// <summary>Gets a value indicating whether the command line leaves out the tools scope.</summary>
    [BvOption("--no-tools")]
    [Description("Leave the .NET local tools alone.")]
    public bool NoTools { get; init; }

    /// <summary>Gets a value indicating whether the command line leaves out the packages scope.</summary>
    [BvOption("--no-packages")]
    [Description("Leave the NuGet package pins alone.")]
    public bool NoPackages { get; init; }

    /// <summary>Gets a value indicating whether the run reports what it would do and changes nothing.</summary>
    [BvOption("--check")]
    [Description("Report what would change, change nothing, and exit 1 when anything would.")]
    public bool Check { get; init; }

    /// <summary>Gets a value indicating whether the report lists the pins that are up to date as well.</summary>
    [BvOption("--all")]
    [Description("List every pin in the report, not only the ones with news. Only with --check.")]
    public bool All { get; init; }

    /// <summary>Gets the version the invocation states, or <see langword="null"/> when it states none.</summary>
    [BvOption("--to <VERSION>")]
    [Description("Set the named pins to this version, whatever their policy says. Downgrades included.")]
    public NuGetVersion? To { get; init; }

    /// <summary>Gets the scopes the command line names to manage, in scope order.</summary>
    public IReadOnlyList<DependencyScope> Included => DependencyScopeFlags.Of(NetSdk, Sdks, Tools, Packages);

    /// <summary>Gets the scopes the command line names to leave out, in scope order.</summary>
    public IReadOnlyList<DependencyScope> Excluded => DependencyScopeFlags.Of(NoNetSdk, NoSdks, NoTools, NoPackages);

    /// <summary>
    /// Parses the command's option tokens into a <see cref="DependenciesUpdateSettings"/>. Unknown options
    /// have already been rejected by <c>CommandArgumentValidator</c>, so every option token is one the
    /// command declares.
    /// </summary>
    /// <param name="positionals">The positional tokens for the command (from
    /// <c>CommandParameters.Positionals</c>).</param>
    /// <param name="options">The option tokens for the command (from <c>CommandParameters.Options</c>).</param>
    /// <returns>The parsed settings.</returns>
    /// <exception cref="BuildFailedException">The command line does not go together, or the version it
    /// states does not parse.</exception>
    public static DependenciesUpdateSettings Parse(IReadOnlyList<string> positionals, IReadOnlyList<string> options)
    {
        Guard.IsNotNull(positionals);
        Guard.IsNotNull(options);
        var reader = new CliOptionReader(options);
        var settings = new DependenciesUpdateSettings
        {
            Filters = [.. positionals],
            NetSdk = reader.ReadFlag("--netsdk"),
            Sdks = reader.ReadFlag("--sdks"),
            Tools = reader.ReadFlag("--tools"),
            Packages = reader.ReadFlag("--packages"),
            NoNetSdk = reader.ReadFlag("--no-netsdk"),
            NoSdks = reader.ReadFlag("--no-sdks"),
            NoTools = reader.ReadFlag("--no-tools"),
            NoPackages = reader.ReadFlag("--no-packages"),
            Check = reader.ReadFlag("--check"),
            All = reader.ReadFlag("--all"),
            To = ParseVersion(reader.ReadValue("--to")),
        };

        Validate(settings);
        return settings;
    }

    private static NuGetVersion? ParseVersion(string? text)
    {
        if (text is null)
        {
            return null;
        }

        return NuGetVersion.TryParse(text, out var version)
            ? version
            : throw new BuildFailedException(ExitCodes.Usage, $"'{text}' is not a version.");
    }

    private static void Validate(DependenciesUpdateSettings settings)
    {
        // An apply run lists what it changed, and what it left alone is the report of `bv dependencies show`.
        if (settings.All && !settings.Check)
        {
            throw new BuildFailedException(
                ExitCodes.Usage,
                "--all lists what a check run would otherwise leave out, so it goes with --check.");
        }

        // A stated version is an edit, and a check run makes none.
        if (settings.To is not null && settings.Check)
        {
            throw new BuildFailedException(ExitCodes.Usage, "--to states a version to write, so it does not go with --check.");
        }

        // The .NET SDK has no package id, so nothing that filters by id can be about it.
        if (settings.NetSdk && settings.Filters.Count > 0)
        {
            throw new BuildFailedException(
                ExitCodes.Usage,
                "The .NET SDK has no package id, so --netsdk does not go with an argument naming pins.");
        }

        if (settings.To is not null && settings.Filters.Count > 1)
        {
            throw new BuildFailedException(ExitCodes.Usage, "--to states the version of one package id, so it takes one argument at most.");
        }
    }
}
