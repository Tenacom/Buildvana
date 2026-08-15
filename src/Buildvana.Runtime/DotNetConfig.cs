// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// The resolved configuration for invocations of the <c>dotnet</c> CLI.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record DotNetConfig
{
    // The single source of the built-in build configuration: ReleaseConfig.Configuration initializes from it
    // too, so the two defaults cannot drift apart.
    internal const string DefaultConfiguration = "Release";

    /// <summary>
    /// Gets the default build configuration passed to <c>dotnet</c>.
    /// </summary>
    public string Configuration { get; init; } = DefaultConfiguration;

    /// <summary>
    /// Gets the invocation configuration common to all <c>dotnet</c> commands (<c>dotnet.all</c>), as
    /// written; the per-command members below already include it.
    /// </summary>
    public DotNetInvocationConfig All { get; init; } = new();

    /// <summary>
    /// Gets the fully resolved invocation configuration for the <c>dotnet restore</c> command:
    /// the common tier, the command's own configuration, and any arguments forwarded after <c>--</c>.
    /// </summary>
    public DotNetInvocationConfig Restore { get; init; } = new();

    /// <summary>
    /// Gets the fully resolved invocation configuration for the <c>dotnet build</c> command:
    /// the common tier, the command's own configuration, and any arguments forwarded after <c>--</c>.
    /// </summary>
    public DotNetInvocationConfig Build { get; init; } = new();

    /// <summary>
    /// Gets the fully resolved invocation configuration for the <c>dotnet test</c> command:
    /// the common tier, the command's own configuration, and any arguments forwarded after <c>--</c>.
    /// </summary>
    public DotNetInvocationConfig Test { get; init; } = new();

    /// <summary>
    /// Gets the fully resolved invocation configuration for the <c>dotnet pack</c> command:
    /// the common tier, the command's own configuration, and any arguments forwarded after <c>--</c>.
    /// </summary>
    public DotNetInvocationConfig Pack { get; init; } = new();

    /// <summary>
    /// Gets the fully resolved invocation configuration for the <c>dotnet nuget push</c> command:
    /// the common tier and the command's own configuration. Forwarded arguments never reach it.
    /// </summary>
    public DotNetInvocationConfig NugetPush { get; init; } = new();
}
