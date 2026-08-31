// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Applies what a run resolved, scope by scope, in the order the scopes must be written in.
/// </summary>
/// <remarks>
/// <para>The <c>packages</c> scope goes first, then <c>tools</c>, then <c>sdks</c>. The <c>netsdk</c> scope
/// is written apart, and last of everything: see <see cref="ApplyNetSdk"/>.</para>
/// <para>A step that fails stops the run and leaves what earlier steps wrote in place. A rerun reads the
/// repository as it stands, finds those pins up to date, and resumes from there.</para>
/// </remarks>
internal sealed class DependencyApplier(
    PackagePinWriter packages,
    ToolPinUpdater tools,
    SdkPinWriter sdks,
    NetSdkPinWriter netSdk)
{
    /// <summary>
    /// Applies the pins of every selected scope but <c>netsdk</c>.
    /// </summary>
    /// <param name="resolution">What the run made of every pin.</param>
    /// <param name="scopes">The scopes the invocation selected.</param>
    /// <param name="cancellationToken">A token that, when signalled, stops before the next step.</param>
    /// <returns>A task representing the ongoing operation.</returns>
    /// <exception cref="BuildFailedException">A file could not be written, or a tool update failed.</exception>
    public async Task ApplyPinsAsync(
        DependencyResolution resolution,
        IReadOnlySet<DependencyScope> scopes,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(resolution);
        Guard.IsNotNull(scopes);
        if (scopes.Contains(DependencyScope.Packages))
        {
            packages.Write(resolution.Packages);
        }

        if (scopes.Contains(DependencyScope.Tools))
        {
            await tools.UpdateAsync(resolution.Tools, cancellationToken).ConfigureAwait(false);
        }

        if (scopes.Contains(DependencyScope.Sdks))
        {
            sdks.Write(resolution.Sdks);
        }
    }

    /// <summary>
    /// Writes the .NET SDK baseline, which goes after every step that spawns <c>dotnet</c>.
    /// </summary>
    /// <param name="resolution">What the run made of every pin.</param>
    /// <param name="scopes">The scopes the invocation selected.</param>
    /// <exception cref="BuildFailedException"><c>global.json</c> could not be written.</exception>
    public void ApplyNetSdk(DependencyResolution resolution, IReadOnlySet<DependencyScope> scopes)
    {
        Guard.IsNotNull(resolution);
        Guard.IsNotNull(scopes);
        if (scopes.Contains(DependencyScope.NetSdk) && resolution.NetSdk is { } baseline)
        {
            netSdk.Write(baseline);
        }
    }
}
