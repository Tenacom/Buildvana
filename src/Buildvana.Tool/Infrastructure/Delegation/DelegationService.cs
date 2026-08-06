// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Json;
using Buildvana.Core.Process;
using Buildvana.Tool.Services;
using Buildvana.Tool.Utilities;
using NuGet.Versioning;

namespace Buildvana.Tool.Infrastructure.Delegation;

/// <summary>
/// Delegates an invocation of a non-local bv to the version pinned in the repository's tool manifest, the way
/// the Angular CLI's global <c>ng</c> always hands over to a project-local install.
/// </summary>
/// <remarks>
/// <para>The decision combines a version comparison with the detected <see cref="InstallLayout"/>: a version
/// mismatch with the manifest pin always delegates (a manifest-run bv matches the pin by construction, so a
/// mismatched bv cannot be the manifest's — and the delegated child, which does match, never delegates again);
/// on a version match, only a confidently local bv (<see cref="InstallLayout.PackageCache"/>) runs in place,
/// so that the manifest's install always runs no matter how bv was launched.</para>
/// <para>Delegation runs <c>dotnet tool restore</c> and then hands the original arguments, unparsed and
/// unvalidated, to <c>dotnet tool run bv</c> with inherited standard streams, forwarding the child's exit code.
/// The configuration file is likewise never read on this side: arguments and configuration may be valid for the
/// pinned version and not for this one, and judging them is the pinned version's job.</para>
/// </remarks>
/// <param name="jsonHelper">The JSON helper used to read the tool manifest.</param>
/// <param name="processRunner">The process runner used for <c>dotnet tool restore</c> and the delegated run.</param>
/// <param name="ownVersion">The version of the running bv.</param>
/// <param name="output">The writer for the delegation info line and warnings, typically standard error: it is
/// narration about how the command runs, not command output, and must not dirty a piped standard output.</param>
internal sealed class DelegationService(
    IJsonHelper jsonHelper,
    IProcessRunner processRunner,
    NuGetVersion ownVersion,
    TextWriter output)
{
    /// <summary>
    /// The environment variable set on the delegated child, carrying the delegating bv's version. Its presence
    /// makes the child run in place unconditionally, so that a mis-detected layout can never delegate in a loop.
    /// </summary>
    public const string DelegatedEnvVar = "BV_DELEGATED";

    // Subcommands that always run on the invoked bv. `update` re-pins the repository to the running bv's own
    // version ("bring this repository to me"); delegating it to the version already pinned would make it a no-op.
    private const string UpdateCommandName = "update";

    /// <summary>
    /// Delegates the invocation described by <paramref name="context"/> to the bv version pinned in the
    /// repository's tool manifest, when there is one and this bv should not run in place.
    /// </summary>
    /// <param name="context">The invocation to consider for delegation.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the ongoing operation.</param>
    /// <returns>The delegated invocation's exit code, to be forwarded verbatim; or <see langword="null"/> when
    /// this bv should run in place.</returns>
    /// <exception cref="BuildFailedException"><c>dotnet tool restore</c> failed, so the pinned version cannot
    /// run; the message names the failure.</exception>
    public async Task<int?> TryDelegateAsync(DelegationContext context, CancellationToken cancellationToken = default)
    {
        var runInPlace = context.DelegationMarkerPresent
            || context.SkipDelegation
            || string.Equals(context.Subcommand, UpdateCommandName, StringComparison.OrdinalIgnoreCase);
        if (runInPlace)
        {
            return null;
        }

        if (!HomeDirectoryDiscovery.TryDiscover(context.StartDirectory, out var homeDirectory))
        {
            return null;
        }

        NuGetVersion? pin;
        try
        {
            pin = ToolManifest.ReadBvPin(jsonHelper, homeDirectory);
        }
        catch (BuildFailedException e)
        {
            await output.WriteLineAsync($"Warning: delegation skipped, the tool manifest could not be read: {e.Message}").ConfigureAwait(false);
            return null;
        }

        if (pin is null)
        {
            return null;
        }

        var pinMatches = VersionComparer.VersionRelease.Equals(pin, ownVersion);
        if (pinMatches && context.InstallLayout == InstallLayout.PackageCache)
        {
            return null;
        }

        if (!pinMatches)
        {
            await output.WriteLineAsync($"Delegating to bv {pin.ToNormalizedString()} from this repository's tool manifest.").ConfigureAwait(false);
        }

        await RestoreToolsAsync(pin, homeDirectory, cancellationToken).ConfigureAwait(false);
        return await processRunner.RunWithInheritedStdioAsync(
            DotNetMuxer.Path,
            ["tool", "run", ToolManifest.BvPackageId, "--", ..context.RawArgs],
            environment: new Dictionary<string, string?> { [DelegatedEnvVar] = ownVersion.ToNormalizedString() },
            workingDirectory: homeDirectory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    // Makes sure the pinned bv is restored before `dotnet tool run` resolves it. Always run: probing whether a
    // restore is needed would mean relying on cache layouts even less contractual than the install layouts, and
    // a satisfied restore is quick and offline.
    private async Task RestoreToolsAsync(NuGetVersion pin, string homeDirectory, CancellationToken cancellationToken)
    {
        try
        {
            _ = await processRunner.RunAsync(
                DotNetMuxer.Path,
                ["tool", "restore"],
                workingDirectory: homeDirectory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (BuildFailedException e)
        {
            throw new BuildFailedException(
                $"Cannot delegate to bv {pin.ToNormalizedString()}: 'dotnet tool restore' failed. {e.Message}",
                e);
        }
    }
}
