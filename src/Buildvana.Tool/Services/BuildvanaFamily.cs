// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Linq;

namespace Buildvana.Tool.Services;

/// <summary>
/// The closed list of packages released in lockstep as the Buildvana family: the bv tool, the Buildvana SDK,
/// and the Buildvana.Runtime library. <c>bv self-update</c> stamps one version into every family pin it can
/// find, and <c>bv deps</c> (once it exists) will treat family pins as invisible; both key on this list.
/// </summary>
/// <remarks>
/// Membership is deliberately a closed list, not a <c>Buildvana.*</c> prefix match: a third-party package
/// under that prefix (e.g. a plugin) must not be forced into lockstep with the family.
/// </remarks>
internal static class BuildvanaFamily
{
    /// <summary>
    /// The ID of bv's own NuGet package, which is also its tool command name.
    /// </summary>
    public const string ToolPackageId = "bv";

    /// <summary>
    /// The ID of the Buildvana SDK package, which is also its MSBuild project SDK name.
    /// </summary>
    public const string SdkPackageId = "Buildvana.Sdk";

    /// <summary>
    /// The ID of the Buildvana.Runtime package, consumed by repository-owned hooks.
    /// </summary>
    public const string RuntimePackageId = "Buildvana.Runtime";

    private static readonly string[] PackageIds = [ToolPackageId, SdkPackageId, RuntimePackageId];

    /// <summary>
    /// Determines whether a package id belongs to the family. NuGet package ids are case-insensitive, and so
    /// is this test.
    /// </summary>
    /// <param name="packageId">The package id to test.</param>
    /// <returns><see langword="true"/> if <paramref name="packageId"/> is a family member; otherwise,
    /// <see langword="false"/>.</returns>
    public static bool Contains(string packageId) => PackageIds.Contains(packageId, StringComparer.OrdinalIgnoreCase);
}
