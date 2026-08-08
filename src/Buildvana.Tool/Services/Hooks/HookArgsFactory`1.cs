// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using Buildvana.Core.HomeDirectory;
using Buildvana.Runtime;
using Buildvana.Tool.Infrastructure;
using Buildvana.Tool.Infrastructure.Delegation;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Hooks;

/// <summary>
/// The base type for hook args factories: provides the assembly of the args properties shared by every
/// hook (see <see cref="HookArgs"/>), so that a derived factory only assembles the properties specific
/// to its hook.
/// </summary>
/// <typeparam name="TArgs">The type of the hook args the factory creates.</typeparam>
/// <param name="home">The home directory provider.</param>
internal abstract class HookArgsFactory<TArgs>(IHomeDirectoryProvider home)
    where TArgs : HookArgs, IHookEvent
{
    /// <summary>
    /// Creates the <see cref="Buildvana.Runtime.RuntimeInfo"/> section shared by every hook's args:
    /// the running bv's version, the delegating bv's version when the run was delegated, and the
    /// absolute paths of the run's well-known directories.
    /// </summary>
    /// <param name="artifactsPath">The path of the directory containing the build artifacts,
    /// either absolute or relative to the current directory.</param>
    /// <returns>A newly-created <see cref="Buildvana.Runtime.RuntimeInfo"/> instance.</returns>
    protected RuntimeInfo CreateRuntimeInfo(string artifactsPath)
    {
        Guard.IsNotNullOrEmpty(artifactsPath);
        var homeDirectory = home.HomeDirectory;
        return new()
        {
            Version = OwnVersion.Value.ToNormalizedString(),
            DelegatingVersion = Environment.GetEnvironmentVariable(DelegationService.DelegatedEnvVar),
            HomeDirectory = homeDirectory,
            ArtifactsDirectory = Path.GetFullPath(artifactsPath),
            ScratchDirectory = Path.GetFullPath(CommonPaths.Scratch, homeDirectory),
        };
    }
}
