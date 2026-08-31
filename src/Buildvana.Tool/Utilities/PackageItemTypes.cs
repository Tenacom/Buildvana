// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Buildvana.Tool.Utilities;

/// <summary>
/// The MSBuild item types a repository states its package pins as.
/// </summary>
internal static class PackageItemTypes
{
    /// <summary>
    /// Gets the item types every repository uses: the central pins of central package management, its
    /// globally applied references, and the references of a project that manages versions itself. A
    /// repository may state pins under item types of its own as well, which it declares as additional
    /// package groups.
    /// </summary>
    public static IReadOnlyList<string> BuiltIn { get; } = ["PackageVersion", "GlobalPackageReference", "PackageReference"];
}
