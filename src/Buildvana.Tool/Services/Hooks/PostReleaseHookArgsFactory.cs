// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Buildvana.Core.Configuration;
using Buildvana.Core.HomeDirectory;
using Buildvana.Runtime;
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
internal sealed class PostReleaseHookArgsFactory(
    IHomeDirectoryProvider home,
    BuildvanaJsonConfigProvider configProvider,
    BuildvanaConfig configuration)
    : HookArgsFactory<PostReleaseHookArgs>(home, configProvider, configuration)
{
    /// <summary>
    /// Creates the args for a <c>release/post-release</c> hook run.
    /// </summary>
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
    /// <returns>A newly-created <see cref="PostReleaseHookArgs"/> instance.</returns>
    public PostReleaseHookArgs Create(
        string artifactsPath,
        string simpleVersion,
        string semVer,
        SemanticVersion? latest,
        bool isPrerelease,
        bool isPublicRelease,
        IReadOnlyDictionary<string, string> producedPackages,
        bool dogfooding)
    {
        Guard.IsNotNullOrEmpty(simpleVersion);
        Guard.IsNotNullOrEmpty(semVer);
        Guard.IsNotNull(producedPackages);
        return new()
        {
            RuntimeInfo = CreateRuntimeInfo(artifactsPath),
            Release = new()
            {
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
