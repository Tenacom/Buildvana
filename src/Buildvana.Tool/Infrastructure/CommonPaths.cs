// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Infrastructure;

/// <summary>
/// Provides well-known paths used throughout the build, relative to the repository root.
/// </summary>
public static class CommonPaths
{
    /// <summary>
    /// The path of the directory where build artifacts for all configurations are stored.
    /// </summary>
    public const string AllArtifacts = "artifacts";

    /// <summary>
    /// The path of the directory where test results and coverage reports are stored.
    /// </summary>
    public const string TestResults = "TestResults";

    /// <summary>
    /// The path of the directory where documentation is stored.
    /// </summary>
    public const string Docs = "docs";
}
