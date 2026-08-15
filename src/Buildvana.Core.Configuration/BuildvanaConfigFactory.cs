// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Runtime;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Composes wire models into the resolved domain model: the single place where configuration defaults and
/// precedence are applied.
/// </summary>
/// <remarks>
/// <para>The factory contains no default values of its own: it starts from the domain defaults (a bare
/// <c>new</c> of each domain record) and overlays what the sources state. Scalars resolve as command line
/// over configuration file over domain default; section-specific rules are documented on the composers.</para>
/// </remarks>
public static class BuildvanaConfigFactory
{
    /// <summary>
    /// Composes the resolved Buildvana configuration of a run from its sources.
    /// </summary>
    /// <param name="json">The wire model of the configuration file, or <see langword="null"/> when the
    /// repository has none.</param>
    /// <param name="commandLine">The configuration overrides stated on the command line, or
    /// <see langword="null"/> when the run has no command line (e.g. an MSBuild task host).</param>
    /// <returns>The resolved configuration.</returns>
    public static BuildvanaConfig Create(BuildvanaJsonConfig? json, CommandLineOverrides? commandLine)
    {
        var dotNet = ComposeDotNet(json?.DotNet, commandLine);
        return new BuildvanaConfig
        {
            Release = ComposeRelease(json?.Release, commandLine, dotNet),
            Versioning = ComposeVersioning(json?.Versioning),
            DotNet = dotNet,
            NuGet = ComposeNuGet(json?.NuGet),
            GitHub = ComposeGitHub(json?.GitHub),
            Git = ComposeGit(json?.Git),
        };
    }

    private static DotNetConfig ComposeDotNet(DotNetJsonConfig? json, CommandLineOverrides? commandLine)
    {
        var defaults = new DotNetConfig();
        return new DotNetConfig
        {
            Configuration = commandLine?.Configuration ?? json?.Configuration ?? defaults.Configuration,
            All = ComposeInvocation(json?.All),
            Restore = ComposeInvocation(json?.Restore),
            Build = ComposeInvocation(json?.Build),
            Test = ComposeInvocation(json?.Test),
            Pack = ComposeInvocation(json?.Pack),
            NugetPush = ComposeInvocation(json?.NugetPush),
        };
    }

    private static DotNetInvocationConfig ComposeInvocation(DotNetInvocationJsonConfig? json)
    {
        var defaults = new DotNetInvocationConfig();
        return new DotNetInvocationConfig
        {
            Args = json?.Args ?? defaults.Args,
            Env = json?.Env ?? defaults.Env,
        };
    }

    // release.configuration falls back to the resolved dotnet.configuration, so a release builds with the
    // general build configuration unless one is stated for releases specifically.
    private static ReleaseConfig ComposeRelease(
        ReleaseJsonConfig? json,
        CommandLineOverrides? commandLine,
        DotNetConfig dotNet)
    {
        var defaults = new ReleaseConfig();
        return new ReleaseConfig
        {
            Branches = json?.Branches ?? defaults.Branches,
            Configuration = commandLine?.Configuration ?? json?.Configuration ?? dotNet.Configuration,
            CheckPublicApi = commandLine?.CheckPublicApi ?? json?.CheckPublicApi ?? defaults.CheckPublicApi,
            ChangelogUpdates = json?.ChangelogUpdates ?? defaults.ChangelogUpdates,
            EmptyChangelog = NormalizeBlankToNull(json?.EmptyChangelog),
            Dogfood = commandLine?.Dogfood ?? json?.Dogfood ?? defaults.Dogfood,
        };
    }

    // Text that is all whitespace would substitute nothing for nothing, so it counts as no substitute at all:
    // the release fails with the same actionable message as when release.emptyChangelog is missing.
    private static string? NormalizeBlankToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static VersioningConfig ComposeVersioning(VersioningJsonConfig? json)
    {
        var defaults = new VersioningConfig();
        return new VersioningConfig
        {
            PrereleaseTag = json?.PrereleaseTag,
            AssemblyVersionPrecision = json?.AssemblyVersionPrecision ?? defaults.AssemblyVersionPrecision,
        };
    }

    private static NuGetConfig ComposeNuGet(NuGetJsonConfig? json)
        => new()
        {
            Feeds = new NuGetFeedsConfig
            {
                Prerelease = ComposeFeed(json?.Feeds?.Prerelease),
                Release = ComposeFeed(json?.Feeds?.Release),
            },
        };

    private static NuGetFeedConfig? ComposeFeed(NuGetFeedJsonConfig? json)
        => json is null
            ? null
            : new NuGetFeedConfig
            {
                Source = json.Source,
                ApiKeyEnv = json.ApiKeyEnv,
            };

    // A blank tokenEnv cannot name a variable, so it counts as not stated at all.
    private static GitHubConfig ComposeGitHub(GitHubJsonConfig? json)
    {
        var defaults = new GitHubConfig();
        return new GitHubConfig
        {
            TokenEnv = json?.TokenEnv is { Length: > 0 } name ? name : defaults.TokenEnv,
        };
    }

    private static GitConfig ComposeGit(GitJsonConfig? json)
        => new()
        {
            Identity = json?.Identity is { } identity
                ? new GitIdentityConfig { Name = identity.Name, Email = identity.Email }
                : null,
        };
}
