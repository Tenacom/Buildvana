// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel;
using Buildvana.Tool.CommandLine;
using Buildvana.Tool.Services.Dependencies;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Subcommands;

/// <summary>
/// Options for the <c>dependencies</c> command, parsed from the command's option tokens by
/// <see cref="Parse"/>. Decorated with <see cref="BvOptionAttribute"/>/<see cref="DescriptionAttribute"/>
/// for the help renderer.
/// </summary>
/// <remarks>
/// <para>Two families of options select scopes: the ones naming the scopes to manage, and the ones naming
/// the scopes to leave out. What either family means, and why they do not mix, is
/// <see cref="DependencyScopeSelection"/>'s business.</para>
/// </remarks>
internal sealed class DependenciesSettings
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

    /// <summary>Gets the scopes the command line names to manage, in scope order.</summary>
    public IReadOnlyList<DependencyScope> Included => ScopesOf(NetSdk, Sdks, Tools, Packages);

    /// <summary>Gets the scopes the command line names to leave out, in scope order.</summary>
    public IReadOnlyList<DependencyScope> Excluded => ScopesOf(NoNetSdk, NoSdks, NoTools, NoPackages);

    /// <summary>
    /// Parses the command's option tokens into a <see cref="DependenciesSettings"/>. Unknown options have
    /// already been rejected by <c>CommandArgumentValidator</c>, so every option token is one the command
    /// declares.
    /// </summary>
    /// <param name="options">The option tokens for the command (from <c>CommandParameters.Options</c>).</param>
    /// <returns>The parsed settings.</returns>
    public static DependenciesSettings Parse(IReadOnlyList<string> options)
    {
        Guard.IsNotNull(options);
        var reader = new CliOptionReader(options);
        return new DependenciesSettings
        {
            NetSdk = reader.ReadFlag("--netsdk"),
            Sdks = reader.ReadFlag("--sdks"),
            Tools = reader.ReadFlag("--tools"),
            Packages = reader.ReadFlag("--packages"),
            NoNetSdk = reader.ReadFlag("--no-netsdk"),
            NoSdks = reader.ReadFlag("--no-sdks"),
            NoTools = reader.ReadFlag("--no-tools"),
            NoPackages = reader.ReadFlag("--no-packages"),
        };
    }

    private static List<DependencyScope> ScopesOf(bool netSdk, bool sdks, bool tools, bool packages)
    {
        var scopes = new List<DependencyScope>();
        if (netSdk)
        {
            scopes.Add(DependencyScope.NetSdk);
        }

        if (sdks)
        {
            scopes.Add(DependencyScope.Sdks);
        }

        if (tools)
        {
            scopes.Add(DependencyScope.Tools);
        }

        if (packages)
        {
            scopes.Add(DependencyScope.Packages);
        }

        return scopes;
    }
}
