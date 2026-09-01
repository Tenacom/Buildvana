// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.Configuration;
using CommunityToolkit.Diagnostics;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Answers every pin of an inventory against the package sources and the .NET release index.
/// </summary>
/// <remarks>
/// <para>Resolution precedes application, so that a run either has a target for every pin it manages or
/// changes nothing at all. A pin naming a package or a version no source has is the repository's own error,
/// and one run reports every one of them: fixing them one failed run at a time would be its own chore.</para>
/// <para>A pin nothing can be resolved for costs no lookup. An unmanaged pin has no version to move, and a
/// pin whose policy is <c>disable</c> is one the repository has frozen on purpose.</para>
/// </remarks>
internal sealed class DependencyResolver(
    IPackageVersionSource packageVersions,
    INetSdkReleaseSource netSdkReleases,
    EffectivePolicyResolver policies)
{
    /// <summary>
    /// Resolves every pin of an inventory.
    /// </summary>
    /// <param name="inventory">What the repository pins.</param>
    /// <param name="request">What the invocation asks of resolution.</param>
    /// <param name="cancellationToken">A token that, when signalled, abandons the resolution.</param>
    /// <returns>What the run made of every pin.</returns>
    /// <exception cref="BuildFailedException">A source could not be reached, no package source is
    /// configured, or the repository states pins the sources do not know. The last carries one diagnostic
    /// per pin.</exception>
    public async Task<DependencyResolution> ResolveAsync(
        DependencyInventory inventory,
        DependencyResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(inventory);
        Guard.IsNotNull(request);
        var errors = new List<BuildDiagnostic>();
        await EnsureStatedVersionExistsAsync(request, errors, cancellationToken).ConfigureAwait(false);
        var netSdk = inventory.NetSdk is null
            ? null
            : await ResolveNetSdkAsync(inventory.NetSdk, request, errors, cancellationToken).ConfigureAwait(false);
        var sdks = await ResolveAllAsync(inventory.Sdks, request, errors, cancellationToken).ConfigureAwait(false);
        var tools = await ResolveAllAsync(inventory.Tools, request, errors, cancellationToken).ConfigureAwait(false);
        var packages = await ResolveAllAsync(inventory.Packages, request, errors, cancellationToken).ConfigureAwait(false);
        var resolution = new DependencyResolution
        {
            NetSdk = netSdk,
            Sdks = sdks,
            Tools = tools,
            Packages = packages,
        };

        EnsureStatedVersionHasAPin(request, resolution, errors);
        if (errors.Count > 0)
        {
            throw new BuildFailedException("Some pins cannot be resolved, so nothing was updated.", errors);
        }

        return resolution;
    }

    private static BuildDiagnostic Error(string code, string message, string? file = null)
        => new(BuildDiagnosticSeverity.Error, code, message, file);

    // The id a stated version is for must have a pin the run can write. A family id never has one: the
    // family moves in lockstep, and one command moves it.
    private static void EnsureStatedVersionHasAPin(
        DependencyResolutionRequest request,
        DependencyResolution resolution,
        List<BuildDiagnostic> errors)
    {
        if (request.TargetId is not { } id)
        {
            return;
        }

        var pins = resolution.Sdks.Concat(resolution.Tools).Concat(resolution.Packages);
        var hasManagedPin = pins.Any(pin => pin.Pin.Management == PinManagement.Managed
            && string.Equals(pin.Pin.Id, id, StringComparison.OrdinalIgnoreCase));
        if (hasManagedPin)
        {
            return;
        }

        var message = BuildvanaFamily.Contains(id)
            ? $"{id} belongs to Buildvana's own package family, which moves in lockstep. Use bv self-update."
            : $"No pin bv manages, in the selected scopes, has the id {id}.";
        errors.Add(Error(DiagnosticCodes.NoSuchPin, message));
    }

    private static PinResolutionState StateOf(TargetSelectionOutcome outcome)
        => outcome switch
        {
            TargetSelectionOutcome.Update => PinResolutionState.Updated,
            TargetSelectionOutcome.UpToDate => PinResolutionState.UpToDate,
            TargetSelectionOutcome.Disabled => PinResolutionState.Disabled,
            _ => PinResolutionState.Held,
        };

    private static PinResolution NewResolution(DependencyPin pin, PackageUpdatePolicy policy, PinResolutionState state, string note)
        => new()
        {
            Pin = pin,
            Policy = policy,
            State = state,
            Note = note,
        };

    private static NetSdkResolution NewNetSdkResolution(
        NetSdkPin pin,
        NetSdkUpdatePolicy policy,
        PinResolutionState state,
        bool writesAllowPrerelease,
        string note)
        => new()
        {
            Pin = pin,
            Policy = policy,
            State = state,
            WritesAllowPrerelease = writesAllowPrerelease,
            Note = note,
        };

    // Every pin of a scope the invocation selected is answered for, whether or not a filter names it: a hook
    // deriving state from a particular pin must see that pin in every run, if only as skipped.
    private async Task<IReadOnlyList<PinResolution>> ResolveAllAsync(
        IReadOnlyList<DependencyPin> pins,
        DependencyResolutionRequest request,
        List<BuildDiagnostic> errors,
        CancellationToken cancellationToken)
    {
        var resolutions = new List<PinResolution>(pins.Count);
        foreach (var pin in pins)
        {
            resolutions.Add(request.Names(pin.Id)
                ? await ResolvePinAsync(pin, request, errors, cancellationToken).ConfigureAwait(false)
                : NewResolution(pin, policies.Resolve(pin), PinResolutionState.Skipped, string.Empty));
        }

        return resolutions;
    }

    private async Task<PinResolution> ResolvePinAsync(
        DependencyPin pin,
        DependencyResolutionRequest request,
        List<BuildDiagnostic> errors,
        CancellationToken cancellationToken)
    {
        var policy = policies.Resolve(pin);
        if (pin.Management != PinManagement.Managed || pin.Version is not { } current)
        {
            return NewResolution(pin, policy, PinResolutionState.Unmanaged, PinNotes.For(pin, policy));
        }

        // A stated version is the user's own edit: it overrules the policy, a disabled one included, and it
        // is the one move that may lower a pin. It reaches a pin of an id-shaped scope only when an argument
        // names the id it is for; with no argument the version is the .NET SDK baseline's.
        if (request.TargetId is not null && request.To is { } stated)
        {
            return VersionComparer.VersionRelease.Equals(stated, current)
                ? NewResolution(pin, policy, PinResolutionState.UpToDate, string.Empty)
                : NewResolution(pin, policy, PinResolutionState.Updated, string.Empty) with { Target = stated };
        }

        if (policy.Kind == PackageUpdatePolicyKind.Disable)
        {
            return NewResolution(pin, policy, PinResolutionState.Disabled, PinNotes.For(pin, policy));
        }

        EnsureSourcesAreConfigured();
        var catalog = await packageVersions.GetVersionsAsync(pin.Id, cancellationToken).ConfigureAwait(false);
        if (catalog.Listed.Count == 0 && catalog.Unlisted.Count == 0)
        {
            errors.Add(Error(DiagnosticCodes.UnknownPackage, $"No configured package source knows {pin.Id}.", pin.DeclaringFile));
            return NewResolution(pin, policy, PinResolutionState.Skipped, string.Empty);
        }

        if (!catalog.Knows(current))
        {
            errors.Add(Error(
                DiagnosticCodes.UnknownVersion,
                $"No configured package source has {pin.Id} {pin.VersionText}.",
                pin.DeclaringFile));
            return NewResolution(pin, policy, PinResolutionState.Skipped, string.Empty);
        }

        // A delisted pin is resolved like any other. Delisting often means the version is vulnerable, so
        // moving away from it is the remedy, and refusing to would be perverse.
        var selection = UpdatePolicyEngine.Select(current, catalog.Listed, policy);
        return new PinResolution
        {
            Pin = pin,
            Policy = policy,
            State = StateOf(selection.Outcome),
            Target = selection.Target,
            LatestStable = selection.LatestStable,
            LatestPreview = selection.LatestPreview,
            Note = catalog.IsListed(current) ? PinNotes.For(pin, policy) : PinNotes.Delisted,
        };
    }

    private async Task<NetSdkResolution> ResolveNetSdkAsync(
        NetSdkPin pin,
        DependencyResolutionRequest request,
        List<BuildDiagnostic> errors,
        CancellationToken cancellationToken)
    {
        var policy = policies.ResolveNetSdk();
        var note = PinNotes.ForNetSdk(pin, policy);
        var writes = pin.AllowPrerelease != policy.AllowPrerelease;
        if (pin.Management != PinManagement.Managed || pin.Version is not { } current)
        {
            return NewNetSdkResolution(pin, policy, PinResolutionState.Unmanaged, writes, note);
        }

        // A stated version is the user's own edit, and the baseline takes it whatever the policy says.
        if (request.To is { } stated && request.TargetId is null)
        {
            return await ResolveStatedNetSdkAsync(pin, policy, stated, current, writes, note, errors, cancellationToken)
                .ConfigureAwait(false);
        }

        if (policy.Kind == NetSdkUpdatePolicyKind.Disable)
        {
            // A disabled scope is skipped whole: bv states what global.json says and writes nothing at all.
            return NewNetSdkResolution(pin, policy, PinResolutionState.Disabled, writesAllowPrerelease: false, note);
        }

        var releases = await netSdkReleases.GetReleasesAsync(current, cancellationToken).ConfigureAwait(false);
        if (!releases.Any(release => VersionComparer.VersionRelease.Equals(release.Version, current)))
        {
            errors.Add(Error(
                DiagnosticCodes.UnknownNetSdkVersion,
                $"The .NET release index has no .NET SDK {pin.VersionText}.",
                GlobalJsonPinReader.RelativePath));
            return NewNetSdkResolution(pin, policy, PinResolutionState.Skipped, writes, note);
        }

        var selection = UpdatePolicyEngine.Select(current, releases, policy);
        return new NetSdkResolution
        {
            Pin = pin,
            Policy = policy,
            State = StateOf(selection.Outcome),
            WritesAllowPrerelease = writes,
            Target = selection.Target,
            LatestStable = selection.LatestStable,
            LatestPreview = selection.LatestPreview,
            Note = note,
        };
    }

    private async Task<NetSdkResolution> ResolveStatedNetSdkAsync(
        NetSdkPin pin,
        NetSdkUpdatePolicy policy,
        NuGetVersion stated,
        NuGetVersion current,
        bool writes,
        string note,
        List<BuildDiagnostic> errors,
        CancellationToken cancellationToken)
    {
        var releases = await netSdkReleases.GetReleasesAsync(stated, cancellationToken).ConfigureAwait(false);
        if (!releases.Any(release => VersionComparer.VersionRelease.Equals(release.Version, stated)))
        {
            errors.Add(Error(
                DiagnosticCodes.UnknownNetSdkVersion,
                $"The .NET release index has no .NET SDK {stated.ToNormalizedString()}.",
                GlobalJsonPinReader.RelativePath));
            return NewNetSdkResolution(pin, policy, PinResolutionState.Skipped, writes, note);
        }

        if (VersionComparer.VersionRelease.Equals(stated, current))
        {
            return NewNetSdkResolution(pin, policy, PinResolutionState.UpToDate, writes, note);
        }

        return NewNetSdkResolution(pin, policy, PinResolutionState.Updated, writes, note) with { Target = stated };
    }

    // The version a run states must exist, whether or not any pin is at it already: it is about to be
    // written into the repository's own files.
    private async Task EnsureStatedVersionExistsAsync(
        DependencyResolutionRequest request,
        List<BuildDiagnostic> errors,
        CancellationToken cancellationToken)
    {
        if (request.To is not { } stated || request.TargetId is not { } id)
        {
            return;
        }

        EnsureSourcesAreConfigured();
        var catalog = await packageVersions.GetVersionsAsync(id, cancellationToken).ConfigureAwait(false);
        if (!catalog.Knows(stated))
        {
            errors.Add(Error(
                DiagnosticCodes.UnknownVersion,
                $"No configured package source has {id} {stated.ToNormalizedString()}."));
        }
    }

    private void EnsureSourcesAreConfigured()
        => BuildFailedException.ThrowIf(
            packageVersions.Sources.Count == 0,
            "No package source is configured, so bv has nowhere to look a version up.");
}
