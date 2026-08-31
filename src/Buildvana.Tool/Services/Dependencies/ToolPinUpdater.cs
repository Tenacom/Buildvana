// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Process;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Moves the .NET local tools a run updates, one at a time.
/// </summary>
/// <remarks>
/// <para>The manifest is never edited here: <c>dotnet tool update</c> writes it and installs the tool, and
/// keeping both halves in the CLI's hands is what makes the manifest agree with what is installed.</para>
/// <para>One tool at a time is forced. <c>--all</c> insists on the latest stable version of every tool, which
/// is a downgrade for a tool pinned to a prerelease line, and it refuses to do it — failing the whole
/// run.</para>
/// </remarks>
internal sealed class ToolPinUpdater(IProcessRunner processRunner, IHomeDirectoryProvider home, IReporter reporter)
{
    /// <summary>
    /// Updates the tools that move.
    /// </summary>
    /// <param name="pins">What the run made of the tool pins. Only the ones that move are updated.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the spawned process.</param>
    /// <returns>A task representing the ongoing operation.</returns>
    /// <exception cref="BuildFailedException">A tool update failed. Nothing after it runs, the remaining
    /// tools included.</exception>
    public async Task UpdateAsync(IReadOnlyList<PinResolution> pins, CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(pins);
        foreach (var pin in PinWriting.Moving(pins))
        {
            var version = pin.Target!.ToNormalizedString();
            reporter.Info($"Updating tool {pin.Pin.Id} to {version}...");
            _ = await processRunner.RunAsync(
                DotNetMuxer.Path,
                ArgumentsFor(pin.Pin.Id, version, IsDowngrade(pin)),
                workingDirectory: home.HomeDirectory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    // The CLI has a downgrade guard of its own, and it stays armed unless the run is an assisted manual
    // edit: a policy-driven update never lowers a pin, so a downgrade the CLI blocks there is a bug caught.
    private static bool IsDowngrade(PinResolution pin)
        => pin.Pin.Version is { } current && VersionComparer.VersionRelease.Compare(pin.Target!, current) < 0;

    private static string[] ArgumentsFor(string id, string version, bool isDowngrade)
        => isDowngrade
            ? ["tool", "update", id, "--local", "--version", version, "--allow-downgrade"]
            : ["tool", "update", id, "--local", "--version", version];
}
