// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// One package a restore resolved, in one target graph of one project.
/// </summary>
/// <param name="TargetGraph">The target graph, named as the assets file names it: a target framework, or a
/// target framework and a runtime identifier.</param>
/// <param name="Id">The package id.</param>
/// <param name="Version">The version the restore resolved.</param>
/// <remarks>
/// <para>A package appears here whether the project references it or receives it through the graph. What
/// the project itself references is stated apart, by <see cref="ProjectAssets.DirectReferences"/>.</para>
/// </remarks>
internal sealed record ResolvedPackage(string TargetGraph, string Id, NuGetVersion Version);
