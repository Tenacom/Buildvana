// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Buildvana.Core;
using Buildvana.Core.Configuration;
using Buildvana.Tool.Configuration;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services;

/// <summary>
/// Resolves <c>dotnet</c> CLI settings from a <see cref="BuildvanaConfig"/>: the default build configuration and the
/// per-command invocation settings (from the <c>dotnet</c> section), plus the NuGet push target (from the
/// <c>nuget</c> section), since the push is itself a <c>dotnet nuget push</c> invocation.
/// </summary>
internal sealed class DotNetSettings
{
    /// <summary>
    /// The build configuration used when neither the configuration chain nor the command line specifies one.
    /// </summary>
    public const string DefaultConfiguration = "Release";

    private readonly NuGetConfig? _nuget;

    /// <summary>
    /// Initializes a new instance of the <see cref="DotNetSettings"/> class.
    /// </summary>
    /// <param name="config">The Buildvana configuration to read the <c>dotnet</c> and <c>nuget</c> sections from.</param>
    public DotNetSettings(BuildvanaConfig config)
    {
        Guard.IsNotNull(config);
        Configuration = config.DotNet?.Configuration ?? DefaultConfiguration;
        Invocations = new DotNetInvocationsSettings(config.DotNet);
        _nuget = config.NuGet;
    }

    /// <summary>Gets the default build configuration (<c>dotnet.configuration</c>, or <c>"Release"</c>).</summary>
    public string Configuration { get; }

    /// <summary>Gets the per-command <c>dotnet</c> invocation settings (the <c>dotnet</c> section).</summary>
    public DotNetInvocationsSettings Invocations { get; }

    /// <summary>
    /// Resolves the NuGet push target for the current release from the <c>nuget.feeds</c> section: the
    /// <c>prerelease</c> channel for prerelease versions (falling back to <c>release</c> when absent), or the
    /// <c>release</c> channel otherwise. The API key is read from the environment variable named by the feed's
    /// <c>apiKeyEnv</c> at the moment of resolution.
    /// </summary>
    /// <param name="isPrerelease">Whether the version being pushed is a prerelease.</param>
    /// <returns>The resolved push target.</returns>
    /// <exception cref="BuildFailedException">No applicable feed is configured, the feed lacks a <c>source</c> or
    /// <c>apiKeyEnv</c>, or the environment variable named by <c>apiKeyEnv</c> is not set or empty.</exception>
    public NuGetPushTarget ResolvePushTarget(bool isPrerelease)
    {
        var feeds = _nuget?.Feeds;
        string channel;
        NuGetFeedConfig? feed;
        if (isPrerelease && GetFeed(feeds, "prerelease") is { } prerelease)
        {
            channel = "prerelease";
            feed = prerelease;
        }
        else
        {
            channel = "release";
            feed = GetFeed(feeds, "release");
        }

        if (feed is null)
        {
            throw new BuildFailedException(
                "No NuGet feed is configured. Set 'nuget.feeds.release' (and optionally 'nuget.feeds.prerelease') in the configuration file.");
        }

        var source = feed.Source is { Length: > 0 } s
            ? s
            : throw new BuildFailedException($"NuGet feed 'nuget.feeds.{channel}' has no source. Set its 'source' in the configuration file.");
        var apiKeyEnv = feed.ApiKeyEnv is { Length: > 0 } k
            ? k
            : throw new BuildFailedException($"NuGet feed 'nuget.feeds.{channel}' has no apiKeyEnv. Set its 'apiKeyEnv' in the configuration file.");
        return new NuGetPushTarget(source, EnvVarHelper.Require(apiKeyEnv));
    }

    private static NuGetFeedConfig? GetFeed(IReadOnlyDictionary<string, NuGetFeedConfig>? feeds, string channel)
        => feeds is not null && feeds.TryGetValue(channel, out var feed) ? feed : null;
}
