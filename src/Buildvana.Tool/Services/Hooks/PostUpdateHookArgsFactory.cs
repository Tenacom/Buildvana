// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Buildvana.Core.Configuration;
using Buildvana.Core.HomeDirectory;
using Buildvana.Runtime;
using Buildvana.Tool.Infrastructure;
using Buildvana.Tool.Services.Dependencies;
using CommunityToolkit.Diagnostics;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Hooks;

/// <summary>
/// Assembles the args for the <c>deps/post-update</c> hook (see <see cref="PostUpdateHookArgs"/>).
/// </summary>
/// <param name="home">The home directory provider.</param>
/// <param name="configProvider">The provider of the configuration file this run reads.</param>
/// <param name="configuration">The resolved configuration of the run.</param>
/// <remarks>
/// <para>The additional package groups are a section of their own here, where the resolution keeps their
/// pins among the package ones: a hook reads a group by the caption its configuration gives it.</para>
/// </remarks>
internal sealed class PostUpdateHookArgsFactory(
    IHomeDirectoryProvider home,
    BuildvanaJsonConfigProvider configProvider,
    BuildvanaConfig configuration)
    : HookArgsFactory<PostUpdateHookArgs>(home, configProvider, configuration)
{
    /// <summary>
    /// Creates the args for a <c>deps/post-update</c> hook run.
    /// </summary>
    /// <param name="resolution">What the run made of every pin.</param>
    /// <param name="check">Whether the run reports what it would do and changes nothing.</param>
    /// <returns>A newly-created <see cref="PostUpdateHookArgs"/> instance.</returns>
    public PostUpdateHookArgs Create(DependencyResolution resolution, bool check)
    {
        Guard.IsNotNull(resolution);
        return new()
        {
            RuntimeInfo = CreateRuntimeInfo(CommonPaths.AllArtifacts),
            Check = check,
            NetSdk = resolution.NetSdk is { } netSdk ? ResultOf(netSdk) : null,
            Sdks = ResultsOf(resolution.Sdks),
            Tools = ResultsOf(resolution.Tools),
            Packages = ResultsOf(resolution.Packages.Where(static pin => pin.Pin.GroupCaption is null)),
            AdditionalPackages =
            [
                .. resolution.Packages
                    .Where(static pin => pin.Pin.GroupCaption is not null)
                    .GroupBy(static pin => pin.Pin.GroupCaption!)
                    .Select(static group => new AdditionalPackagesResult { Caption = group.Key, Results = ResultsOf(group) }),
            ],
        };
    }

    private static IReadOnlyList<DependencyResult> ResultsOf(IEnumerable<PinResolution> pins) => [.. pins.Select(ResultOf)];

    private static DependencyResult ResultOf(PinResolution pin)
        => new()
        {
            Id = pin.Pin.Id,
            DeclaringFile = pin.Pin.DeclaringFile,
            CurrentVersion = pin.Pin.VersionText,
            Target = Text(pin.Target),
            State = StateOf(pin.State),
            LatestStable = Text(pin.LatestStable),
            LatestPreview = Text(pin.LatestPreview),
            Policy = pin.Policy.ToString(),
        };

    private static DependencyResult ResultOf(NetSdkResolution netSdk)
        => new()
        {
            DeclaringFile = GlobalJsonPinReader.RelativePath,
            CurrentVersion = netSdk.Pin.VersionText,
            Target = Text(netSdk.Target),
            State = StateOf(netSdk.State),
            LatestStable = Text(netSdk.LatestStable),
            LatestPreview = Text(netSdk.LatestPreview),
            Policy = netSdk.Policy.ToString(),
        };

    private static string? Text(NuGetVersion? version) => version?.ToNormalizedString();

    private static DependencyResultState StateOf(PinResolutionState state)
        => state switch
        {
            PinResolutionState.UpToDate => DependencyResultState.UpToDate,
            PinResolutionState.Updated => DependencyResultState.Updated,
            PinResolutionState.Disabled => DependencyResultState.Disabled,
            PinResolutionState.Unmanaged => DependencyResultState.Unmanaged,
            PinResolutionState.Skipped => DependencyResultState.Skipped,
            _ => DependencyResultState.Held,
        };
}
