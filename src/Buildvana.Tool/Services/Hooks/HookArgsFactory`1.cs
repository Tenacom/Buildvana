// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core.Configuration;
using Buildvana.Core.HomeDirectory;
using Buildvana.Runtime;
using Buildvana.Tool.Infrastructure;
using Buildvana.Tool.Infrastructure.Delegation;
using Buildvana.Tool.Services.Solution;
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
/// <param name="configProvider">The provider of the configuration file this run reads.</param>
/// <param name="configuration">The resolved configuration of the run.</param>
/// <param name="solutions">The reader of the model of the solution's projects.</param>
internal abstract class HookArgsFactory<TArgs>(
    IHomeDirectoryProvider home,
    BuildvanaJsonConfigProvider configProvider,
    BuildvanaConfig configuration,
    SolutionInfoReader solutions)
    where TArgs : HookArgs, IHookEvent
{
    /// <summary>
    /// Gets the resolved configuration of the run.
    /// </summary>
    protected BuildvanaConfig Configuration => configuration;

    /// <summary>
    /// Creates the <see cref="Buildvana.Runtime.RuntimeInfo"/> section shared by every hook's args:
    /// the running bv's version, the delegating bv's version when the run was delegated, and the
    /// absolute paths of the run's well-known directories and configuration file.
    /// </summary>
    /// <param name="artifactsPath">The path of the directory containing the build artifacts,
    /// either absolute or relative to the home directory.</param>
    /// <returns>A newly-created <see cref="Buildvana.Runtime.RuntimeInfo"/> instance.</returns>
    /// <remarks>
    /// <para>Every path is resolved against the home directory, so that the assembled args do not depend on
    /// the process's current directory.</para>
    /// </remarks>
    protected RuntimeInfo CreateRuntimeInfo(string artifactsPath)
    {
        Guard.IsNotNullOrEmpty(artifactsPath);
        return new() {
            Version = OwnVersion.Value.ToNormalizedString(),
            DelegatingVersion = Environment.GetEnvironmentVariable(DelegationService.DelegatedEnvVar),

            // The provider's value carries a trailing separator; hooks get the same form as every other
            // path in the args, and the same form Directory.GetCurrentDirectory() reports to them.
            HomeDirectory = Path.TrimEndingDirectorySeparator(home.HomeDirectory),
            ArtifactsDirectory = home.GetFullPath(artifactsPath),
            ScratchDirectory = home.GetFullPath(CommonPaths.Scratch),

            // Already absolute, and already the file this run read: hooks are told which one it is instead of
            // running their own search and possibly landing on a different answer.
            ConfigFile = configProvider.Path,

            // A snapshot of the run's resolved settings, not a pointer to the file: an args file re-run by
            // hand must show the hook the configuration of the run it belongs to, however the file has
            // changed since.
            Configuration = configuration,
        };
    }

    /// <summary>
    /// Reads the <see cref="SolutionInfo"/> section shared by every hook's args: the solution's projects, as
    /// MSBuild evaluates them in the given build configuration.
    /// </summary>
    /// <param name="buildConfiguration">The build configuration of the command, such as
    /// <see cref="ReleaseConfig.Configuration"/> for <c>bv release</c>.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the evaluation.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the ongoing operation, whose result is the model of the
    /// solution's projects.</returns>
    protected Task<SolutionInfo> ReadSolutionAsync(string buildConfiguration, CancellationToken cancellationToken)
        => solutions.ReadAsync(buildConfiguration, cancellationToken);
}
