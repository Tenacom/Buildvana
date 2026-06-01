// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Buildvana.Core.Configuration;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services;

/// <summary>
/// Resolves <c>dotnet</c> CLI settings from the <c>dotnet</c> section of a <see cref="BuildvanaConfig"/>:
/// the default build configuration plus the per-command extra arguments and environment variables.
/// </summary>
internal sealed class DotNetSettings
{
    /// <summary>
    /// The build configuration used when neither the configuration chain nor the command line specifies one.
    /// </summary>
    public const string DefaultConfiguration = "Release";

    /// <summary>
    /// Initializes a new instance of the <see cref="DotNetSettings"/> class.
    /// </summary>
    /// <param name="config">The Buildvana configuration to read the <c>dotnet</c> section from.</param>
    public DotNetSettings(BuildvanaConfig config)
    {
        Guard.IsNotNull(config);
        var dotnet = config.DotNet;
        Configuration = dotnet?.Configuration ?? DefaultConfiguration;
        All = Resolve(dotnet?.All);
        Restore = Resolve(dotnet?.Restore);
        Build = Resolve(dotnet?.Build);
        Test = Resolve(dotnet?.Test);
        Pack = Resolve(dotnet?.Pack);
        NugetPush = Resolve(dotnet?.NugetPush);
    }

    /// <summary>Gets the default build configuration (<c>dotnet.configuration</c>, or <c>"Release"</c>).</summary>
    public string Configuration { get; }

    /// <summary>Gets the settings common to every <c>dotnet</c> command (<c>dotnet.all</c>).</summary>
    public DotNetInvocationSettings All { get; }

    /// <summary>Gets the settings for the <c>dotnet restore</c> command (<c>dotnet.restore</c>).</summary>
    public DotNetInvocationSettings Restore { get; }

    /// <summary>Gets the settings for the <c>dotnet build</c> command (<c>dotnet.build</c>).</summary>
    public DotNetInvocationSettings Build { get; }

    /// <summary>Gets the settings for the <c>dotnet test</c> command (<c>dotnet.test</c>).</summary>
    public DotNetInvocationSettings Test { get; }

    /// <summary>Gets the settings for the <c>dotnet pack</c> command (<c>dotnet.pack</c>).</summary>
    public DotNetInvocationSettings Pack { get; }

    /// <summary>Gets the settings for the <c>dotnet nuget push</c> command (<c>dotnet.nugetPush</c>).</summary>
    public DotNetInvocationSettings NugetPush { get; }

    private static DotNetInvocationSettings Resolve(DotNetInvocationConfig? config)
        => config is null
            ? DotNetInvocationSettings.Empty
            : new(config.Args ?? [], config.Env ?? new Dictionary<string, string?>());
}
