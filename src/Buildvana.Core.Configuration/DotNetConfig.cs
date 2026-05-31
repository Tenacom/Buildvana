// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Configures invocations of the <c>dotnet</c> CLI.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record DotNetConfig
{
    /// <summary>
    /// Gets the default build configuration passed to <c>dotnet</c>.
    /// </summary>
    [Description("Default build configuration (e.g. Debug, Release).")]
    public string? Configuration { get; init; }

    /// <summary>
    /// Gets invocation configuration common to all <c>dotnet</c> commands.
    /// </summary>
    [Description("Invocation configuration common to all `dotnet` commands.")]
    public DotNetInvocationConfig? All { get; init; }

    /// <summary>
    /// Gets invocation configuration for the <c>dotnet restore</c> command.
    /// </summary>
    [Description("Invocation configuration for the `dotnet restore` command.")]
    public DotNetInvocationConfig? Restore { get; init; }

    /// <summary>
    /// Gets invocation configuration for the <c>dotnet build</c> command.
    /// </summary>
    [Description("Invocation configuration for the `dotnet build` command.")]
    public DotNetInvocationConfig? Build { get; init; }

    /// <summary>
    /// Gets invocation configuration for the <c>dotnet test</c> command.
    /// </summary>
    [Description("Invocation configuration for the `dotnet test` command.")]
    public DotNetInvocationConfig? Test { get; init; }

    /// <summary>
    /// Gets invocation configuration for the <c>dotnet pack</c> command.
    /// </summary>
    [Description("Invocation configuration for the `dotnet pack` command.")]
    public DotNetInvocationConfig? Pack { get; init; }

    /// <summary>
    /// Gets invocation configuration for the <c>dotnet nuget push</c> command.
    /// </summary>
    [Description("Invocation configuration for the `dotnet nuget push` command.")]
    public DotNetInvocationConfig? NugetPush { get; init; }
}
