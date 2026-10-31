// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core.Configuration;
using Buildvana.Core.HomeDirectory;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Solution;
using CommunityToolkit.Diagnostics;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Hooks;

/// <summary>
/// Assembles the args for the <c>release/post-release</c> hook
/// (see <see cref="PostReleaseHookArgs"/>).
/// </summary>
/// <param name="home">The home directory provider.</param>
/// <param name="configProvider">The provider of the configuration file this run reads.</param>
/// <param name="configuration">The resolved configuration of the run.</param>
/// <param name="solutions">The reader of the model of the solution's projects.</param>
internal sealed class PostReleaseHookArgsFactory(
    IHomeDirectoryProvider home,
    BuildvanaJsonConfigProvider configProvider,
    BuildvanaConfig configuration,
    SolutionInfoReader solutions)
    : HookArgsFactory<PostReleaseHookArgs>(home, configProvider, configuration, solutions)
{
    /// <summary>
    /// Creates the args for a <c>release/post-release</c> hook run.
    /// </summary>
    /// <remarks>
    /// <para>The projects are evaluated in <see cref="ReleaseConfig.Configuration"/>, the configuration the
    /// release builds in.</para>
    /// </remarks>
    /// <param name="artifactsPath">The path of the directory containing the build artifacts,
    /// either absolute or relative to the current directory.</param>
    /// <param name="simpleVersion">The version being released, in simple <c>MAJOR.MINOR.PATCH</c> form.</param>
    /// <param name="semVer">The version being released, in full semantic version form.</param>
    /// <param name="latest">The previously released version, or <see langword="null"/> when no previous
    /// release exists.</param>
    /// <param name="isPrerelease">Whether the version being released is a prerelease.</param>
    /// <param name="isPublicRelease">Whether the release is a public release.</param>
    /// <param name="producedPackages">The packages produced by the release, as a map from package ID to version.</param>
    /// <param name="dogfooding">Whether the built-in self-reference rewrites will run in this release.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the evaluation of the
    /// projects.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the ongoing operation, whose result is a newly-created
    /// <see cref="PostReleaseHookArgs"/> instance.</returns>
    public async Task<PostReleaseHookArgs> CreateAsync(
        string artifactsPath,
        string simpleVersion,
        string semVer,
        SemanticVersion? latest,
        bool isPrerelease,
        bool isPublicRelease,
        IReadOnlyDictionary<string, string> producedPackages,
        bool dogfooding,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNullOrEmpty(simpleVersion);
        Guard.IsNotNullOrEmpty(semVer);
        Guard.IsNotNull(producedPackages);
        var solution = await ReadSolutionAsync(Configuration.Release.Configuration, cancellationToken).ConfigureAwait(false);
        return new() {
            RuntimeInfo = CreateRuntimeInfo(artifactsPath),
            Solution = solution,
            Release = new() {
                Version = simpleVersion,
                SemVer = semVer,
                PreviousVersion = latest?.ToString(),
                IsPrerelease = isPrerelease,
                IsPublicRelease = isPublicRelease,
            },
            ProducedPackages = producedPackages,
            Dogfooding = dogfooding,
        };
    }
}
