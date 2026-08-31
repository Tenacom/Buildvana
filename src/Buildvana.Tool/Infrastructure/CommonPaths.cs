// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Runtime;

namespace Buildvana.Tool.Infrastructure;

/// <summary>
/// Provides well-known paths used throughout the build, relative to the repository root.
/// </summary>
internal static class CommonPaths
{
    /// <summary>
    /// The path of bv's scratch directory, holding machine-generated temporary files.
    /// Repositories are expected to gitignore it; bv never considers its contents
    /// when detecting working-tree changes, whether or not they do.
    /// </summary>
    public const string Scratch = WellKnownPaths.ScratchDirectory;

    /// <summary>
    /// The path of the directory where the Buildvana SDK writes the package pins of the solution's projects,
    /// one file per evaluation, for <c>bv dependencies</c> to read. Emptied at the start of every read.
    /// </summary>
    public const string PinDump = Scratch + "/pin-dump";

    /// <summary>
    /// The path of the directory where build artifacts for all configurations are stored.
    /// </summary>
    public const string AllArtifacts = "artifacts";

    /// <summary>
    /// The path of the directory where test results and coverage reports are stored.
    /// </summary>
    public const string TestResults = "TestResults";
}
