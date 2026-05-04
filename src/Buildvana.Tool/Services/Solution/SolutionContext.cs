// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.SolutionPersistence.Model;

namespace Buildvana.Tool.Services.Solution;

/// <summary>
/// Carries the parsed model and absolute file path of a solution. Path-derived helpers (such as the
/// solution directory and project-path resolution) live in <see cref="SolutionContextExtensions"/>.
/// </summary>
/// <param name="SolutionPath">The absolute path of the solution file.</param>
/// <param name="Model">The parsed solution model.</param>
public sealed record SolutionContext(string SolutionPath, SolutionModel Model);
