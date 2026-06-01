// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using Buildvana.Core.Configuration;

namespace Buildvana.Tool.Services;

/// <summary>
/// The resolved per-command <c>dotnet</c> invocation settings, taken from the <c>dotnet</c> section of a
/// <see cref="BuildvanaConfig"/>: the settings common to every command (<c>dotnet.all</c>) plus the
/// per-command overlays.
/// </summary>
internal sealed class DotNetInvocationsSettings
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DotNetInvocationsSettings"/> class.
    /// </summary>
    /// <param name="dotnet">The <c>dotnet</c> configuration section to read invocation settings from.</param>
    public DotNetInvocationsSettings(DotNetConfig? dotnet)
    {
        All = Resolve(dotnet?.All);
        Restore = Resolve(dotnet?.Restore);
        Build = Resolve(dotnet?.Build);
        Test = Resolve(dotnet?.Test);
        Pack = Resolve(dotnet?.Pack);
        NugetPush = Resolve(dotnet?.NugetPush);
    }

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
            : new(config.Args ?? [], config.Env ?? ReadOnlyDictionary<string, string?>.Empty);
}
