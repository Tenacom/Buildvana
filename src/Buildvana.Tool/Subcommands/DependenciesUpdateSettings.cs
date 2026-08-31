// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel;
using Buildvana.Core;
using Buildvana.Tool.CommandLine;
using Buildvana.Tool.Services.Dependencies;
using CommunityToolkit.Diagnostics;

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

    /// <summary>Gets the scopes the command line names to manage, in scope order.</summary>
    public IReadOnlyList<DependencyScope> Included => DependencyScopeFlags.Of(NetSdk, Sdks, Tools, Packages);

    /// <summary>Gets the scopes the command line names to leave out, in scope order.</summary>
    public IReadOnlyList<DependencyScope> Excluded => DependencyScopeFlags.Of(NoNetSdk, NoSdks, NoTools, NoPackages);

    /// <summary>
    /// Parses the command's option tokens into a <see cref="DependenciesUpdateSettings"/>. Unknown options
    /// have already been rejected by <c>CommandArgumentValidator</c>, so every option token is one the
    /// command declares.
    /// </summary>
    /// <param name="options">The option tokens for the command (from <c>CommandParameters.Options</c>).</param>
    /// <returns>The parsed settings.</returns>
    /// <exception cref="BuildFailedException">The options do not go together.</exception>
    public static DependenciesUpdateSettings Parse(IReadOnlyList<string> options)
    {
        Guard.IsNotNull(options);
        var reader = new CliOptionReader(options);
        var settings = new DependenciesUpdateSettings
        {
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
        };

        // An apply run lists what it changed, and what it left alone is the report of `bv dependencies show`.
        if (settings.All && !settings.Check)
        {
            throw new BuildFailedException(
                ExitCodes.Usage,
                "--all lists what a check run would otherwise leave out, so it goes with --check.");
        }

        return settings;
    }
}
