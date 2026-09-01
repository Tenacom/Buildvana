// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// One entry of a transitive override file.
/// </summary>
/// <param name="PackageId">The package the entry is about.</param>
/// <param name="Version">The version the entry states, or <see langword="null"/> where it states none.</param>
/// <remarks>
/// <para>In the central file an entry always states a version, and it is the version the projects using
/// central package management resolve the package at.</para>
/// <para>In a project's own file an entry promotes the package to a reference of that project. It states a
/// version where the project manages its versions itself, and none where the central file, or a pin the
/// repository wrote, supplies one.</para>
/// </remarks>
internal sealed record PackageOverride(string PackageId, NuGetVersion? Version);
