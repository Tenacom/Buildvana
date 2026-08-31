// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Buildvana.Core;
using Buildvana.Core.Configuration;
using Buildvana.Runtime;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Composes the policy that governs a pin out of everything the repository states about it.
/// </summary>
/// <remarks>
/// <para>The effective policy of a pin is the first one found, trying these in order: the
/// <c>UpdatePolicy</c> metadata of the pin itself, the first matching pattern of
/// <c>dependencies.policies</c>, the policy of the additional package group the pin belongs to, and the
/// policy of its scope. Every pin therefore has one, and <c>bv dependencies show</c> is where a reader sees
/// which.</para>
/// <para>Patterns are tried in the order the configuration file states them, and the first match wins.
/// Order is the only rule: nothing ranks a specific pattern above a general one, so a leading <c>*</c>
/// silences every pattern after it, and every group policy with them. That is the user's own doing, and
/// their own to undo.</para>
/// </remarks>
internal sealed class EffectivePolicyResolver
{
    private readonly DependenciesConfig _config;
    private readonly Dictionary<string, string> _groupPolicies = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="EffectivePolicyResolver"/> class.
    /// </summary>
    /// <param name="config">The resolved dependency configuration.</param>
    public EffectivePolicyResolver(DependenciesConfig config)
    {
        Guard.IsNotNull(config);
        _config = config;
        foreach (var group in config.AdditionalPackages)
        {
            _groupPolicies[group.Caption] = group.Policy;
        }
    }

    /// <summary>
    /// Composes the policy governing a pin of the <c>packages</c>, <c>sdks</c>, or <c>tools</c> scope.
    /// </summary>
    /// <param name="pin">The pin.</param>
    /// <returns>The policy.</returns>
    /// <exception cref="BuildFailedException">The pin states a policy of its own that does not parse.</exception>
    public PackageUpdatePolicy Resolve(DependencyPin pin)
    {
        Guard.IsNotNull(pin);
        if (pin.MetadataPolicy is { } metadataPolicy)
        {
            // Configuration is validated against the schema when it is read, and item metadata is not: this
            // is the one policy string a repository can state without anything having checked it.
            return PackageUpdatePolicy.TryParse(metadataPolicy, out var stated)
                ? stated
                : throw new BuildFailedException(
                    $"'{metadataPolicy}' is not an update policy. {pin.Id} states it as UpdatePolicy metadata in {pin.DeclaringFile}.");
        }

        return Parse(MatchingPattern(pin.Id) ?? GroupPolicy(pin) ?? ScopeDefault(pin.Scope));
    }

    /// <summary>
    /// Composes the policy governing the .NET SDK baseline, which no pin of its own can override.
    /// </summary>
    /// <returns>The policy.</returns>
    public NetSdkUpdatePolicy ResolveNetSdk()
    {
        // The value is one the schema accepts, so it parses; a default stands in for the impossible.
        _ = NetSdkUpdatePolicy.TryParse(_config.Scopes.NetSdk, out var policy);
        return policy;
    }

    // Every value here has been validated against the policy strings its position accepts, so parsing it is
    // reading it back rather than checking it.
    private static PackageUpdatePolicy Parse(string text)
    {
        _ = PackageUpdatePolicy.TryParse(text, out var policy);
        return policy;
    }

    private string? MatchingPattern(string id)
    {
        foreach (var rule in _config.Policies)
        {
            if (PackageIdPattern.Matches(rule.Pattern, id))
            {
                return rule.Policy;
            }
        }

        return null;
    }

    private string? GroupPolicy(DependencyPin pin)
        => pin.GroupCaption is { } caption && _groupPolicies.TryGetValue(caption, out var policy) ? policy : null;

    private string ScopeDefault(DependencyScope scope)
        => scope switch
        {
            DependencyScope.Sdks => _config.Scopes.Sdks,
            DependencyScope.Tools => _config.Scopes.Tools,
            _ => _config.Scopes.Packages,
        };
}
