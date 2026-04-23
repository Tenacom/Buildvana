// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Cake.Core.IO;

namespace Buildvana.Tool.Services;

/// <summary>
/// Provides information about commonly-used paths in the repository.
/// </summary>
public sealed class PathsService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PathsService"/> class.
    /// </summary>
    public PathsService()
    {
        AllArtifacts = new DirectoryPath("artifacts");
        TestResults = new DirectoryPath("TestResults");
        Docs = new DirectoryPath("docs");
    }

    /// <summary>
    /// Gets the path of the directory where build artifacts for all configurations are stored.
    /// </summary>
    public DirectoryPath AllArtifacts { get; }

    /// <summary>
    /// Gets the path of the directory where test results and coverage reports are stored.
    /// </summary>
    public DirectoryPath TestResults { get; }

    /// <summary>
    /// Gets the path of the directory where documentation is stored.
    /// </summary>
    public DirectoryPath Docs { get; }
}
