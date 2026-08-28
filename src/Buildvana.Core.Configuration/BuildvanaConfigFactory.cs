// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
            Dependencies = ComposeDependencies(json?.Dependencies),
            FileBasedApps = ComposeFileBasedApps(json?.FileBasedApps),
        };
    }

    // Per-command invocations come out fully resolved: the common tier (dotnet.all) folds in first, then the
    // command's own configuration, then — for the pipeline commands — the arguments forwarded after `--`,
    // which must win over configured ones and therefore cannot simply be part of dotnet.all, which composes
    // first. `dotnet nuget push` gets no forwarded arguments: `bv release` rejects the `--` separator, and
    // the push's argument surface is not the pipeline's. The All member itself stays as written, for
    // consumers that want the common tier alone.
    private static DotNetConfig ComposeDotNet(DotNetJsonConfig? json, CommandLineOverrides? commandLine)
    {
        var defaults = new DotNetConfig();
        var all = ComposeInvocation(json?.All, common: null, forwardedArgs: null);
        var forwardedArgs = commandLine?.ForwardedArgs;
        return new DotNetConfig
        {
            Configuration = NormalizeBlankToNull(commandLine?.Configuration)
                ?? NormalizeBlankToNull(json?.Configuration)
                ?? defaults.Configuration,
            All = all,
            Restore = ComposeInvocation(json?.Restore, all, forwardedArgs),
            Build = ComposeInvocation(json?.Build, all, forwardedArgs),
            Test = ComposeInvocation(json?.Test, all, forwardedArgs),
            Pack = ComposeInvocation(json?.Pack, all, forwardedArgs),
            NugetPush = ComposeInvocation(json?.NugetPush, all, forwardedArgs: null),
        };
    }

    private static DotNetInvocationConfig ComposeInvocation(
        DotNetInvocationJsonConfig? json,
        DotNetInvocationConfig? common,
        IReadOnlyList<string>? forwardedArgs)
        => new()
        {
            Args = [.. common?.Args ?? [], .. json?.Args ?? [], .. forwardedArgs ?? []],
            Env = ComposeEnv(common?.Env, json?.Env),
        };

    // Later entries override earlier ones by key; a null value is preserved, meaning "remove the variable
    // from the child environment".
    private static Dictionary<string, string?> ComposeEnv(
        IReadOnlyDictionary<string, string?>? common,
        IReadOnlyDictionary<string, string?>? overlay)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, value) in common ?? ReadOnlyDictionary<string, string?>.Empty)
        {
            result[key] = value;
        }

        foreach (var (key, value) in overlay ?? ReadOnlyDictionary<string, string?>.Empty)
        {
            result[key] = value;
        }

        return result;
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
            Configuration = NormalizeBlankToNull(commandLine?.Configuration)
                ?? NormalizeBlankToNull(json?.Configuration)
                ?? dotNet.Configuration,
            CheckPublicApi = commandLine?.CheckPublicApi ?? json?.CheckPublicApi ?? defaults.CheckPublicApi,
            ChangelogUpdates = json?.ChangelogUpdates ?? defaults.ChangelogUpdates,

            // Blank substitute text would substitute nothing for nothing: normalized away, it fails the
            // release with the same actionable message as a missing release.emptyChangelog.
            EmptyChangelog = NormalizeBlankToNull(json?.EmptyChangelog),
            Dogfood = commandLine?.Dogfood ?? json?.Dogfood ?? defaults.Dogfood,
        };
    }

    private static VersioningConfig ComposeVersioning(VersioningJsonConfig? json)
    {
        var defaults = new VersioningConfig();
        return new VersioningConfig
        {
            // A blank tag could never appear in a version, so it counts as not stated: prereleases are
            // not allowed.
            PrereleaseTag = NormalizeBlankToNull(json?.PrereleaseTag),
            AssemblyVersionPrecision = json?.AssemblyVersionPrecision ?? defaults.AssemblyVersionPrecision,
        };
    }

    // A prerelease with no feed of its own pushes to the release feed: the fallback is resolved here, so
    // consumers read the channel they need instead of re-implementing the chain.
    private static NuGetConfig ComposeNuGet(NuGetJsonConfig? json)
    {
        var release = ComposeFeed(json?.Feeds?.Release);
        return new NuGetConfig
        {
            Feeds = new NuGetFeedsConfig
            {
                Prerelease = ComposeFeed(json?.Feeds?.Prerelease) ?? release,
                Release = release,
            },
        };
    }

    // The schema requires source and apiKeyEnv whenever a feed is stated, and forbids blank values for
    // them, so a wire feed that reaches the factory carries both.
    private static NuGetFeedConfig? ComposeFeed(NuGetFeedJsonConfig? json)
        => json is null
            ? null
            : new NuGetFeedConfig
            {
                Source = json.Source!,
                ApiKeyEnv = json.ApiKeyEnv!,
            };

    // A blank tokenEnv cannot name a variable, so it counts as not stated at all.
    private static GitHubConfig ComposeGitHub(GitHubJsonConfig? json)
    {
        var defaults = new GitHubConfig();
        return new GitHubConfig
        {
            TokenEnv = NormalizeBlankToNull(json?.TokenEnv) ?? defaults.TokenEnv,
        };
    }

    // The schema requires name and email whenever git.identity is stated, and forbids blank values for
    // them, so a wire identity that reaches the factory carries both.
    private static GitConfig ComposeGit(GitJsonConfig? json)
        => new()
        {
            Identity = json?.Identity is { } identity
                ? new GitIdentityConfig { Name = identity.Name!, Email = identity.Email! }
                : null,
        };

    private static DependenciesConfig ComposeDependencies(DependenciesJsonConfig? json)
    {
        var scopes = ComposeDependencyScopes(json?.Scopes);
        return new DependenciesConfig
        {
            Scopes = scopes,
            Policies = ComposePolicies(json?.Policies),
            AdditionalPackages = ComposeAdditionalPackages(json?.AdditionalPackages, scopes.Packages),
        };
    }

    private static DependencyScopesConfig ComposeDependencyScopes(DependencyScopesJsonConfig? json)
    {
        var defaults = new DependencyScopesConfig();
        return new DependencyScopesConfig
        {
            NetSdk = NormalizeBlankToNull(json?.NetSdk) ?? defaults.NetSdk,
            Sdks = NormalizeBlankToNull(json?.Sdks) ?? defaults.Sdks,
            Tools = NormalizeBlankToNull(json?.Tools) ?? defaults.Tools,
            Packages = NormalizeBlankToNull(json?.Packages) ?? defaults.Packages,
        };
    }

    // Document order is what decides which rule claims a pin, so it is carried over as written.
    private static IReadOnlyList<UpdatePolicyRule> ComposePolicies(IReadOnlyList<UpdatePolicyRuleJsonConfig>? json)
        => json is null
            ? []
            : [.. json.Select(static rule => new UpdatePolicyRule { Pattern = rule.Pattern, Policy = rule.Policy })];

    private static IReadOnlyList<AdditionalPackagesConfig> ComposeAdditionalPackages(
        IReadOnlyList<AdditionalPackagesJsonConfig>? json,
        string packagesPolicy)
        => json is null ? [] : [.. json.Select(group => ComposeAdditionalPackageGroup(group, packagesPolicy))];

    // A group that states no policy of its own takes the packages scope policy, resolved here rather than
    // left to consumers. It changes no outcome — a policy rule and UpdatePolicy metadata both outrank a
    // group policy either way — and leaves the resolved model with nothing to fall back on. The schema
    // requires files and items whenever a group is stated, so a wire group that reaches here carries both.
    private static AdditionalPackagesConfig ComposeAdditionalPackageGroup(
        AdditionalPackagesJsonConfig json,
        string packagesPolicy)
        => new()
        {
            Caption = json.Caption,
            Files = json.Files,
            Items = json.Items,
            Policy = NormalizeBlankToNull(json.Policy) ?? packagesPolicy,
        };

    // Configured patterns extend the built-in scope rather than replace it: hooks are file-based apps by
    // definition, so no configuration can move them out of scope. The built-in patterns go last because in
    // gitignore syntax the last matching pattern wins: last, they override a configured negation of the hooks
    // scope, and they match nothing outside it, so they can override nothing else.
    private static IReadOnlyList<string> ComposeFileBasedApps(IReadOnlyList<string>? json)
    {
        var defaults = new BuildvanaConfig();
        return json is null ? defaults.FileBasedApps : [.. json, .. defaults.FileBasedApps];
    }

    // Composition has one definition of "blank": text that is null, empty, or all whitespace is not a value.
    // A blank optional member counts as not stated at all, so the next precedence tier applies.
    private static string? NormalizeBlankToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
