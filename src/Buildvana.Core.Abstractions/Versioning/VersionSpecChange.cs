// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.Versioning;

/// <summary>
/// Specifies how to modify the version specification upon publishing a release.
/// </summary>
public enum VersionSpecChange
{
    /// <summary>
    /// Do not force a version increment; do not modify the prerelease marker.
    /// </summary>
    None,

    /// <summary>
    /// Do not force a version increment; mark the version line as prerelease if it is not already.
    /// </summary>
    Unstable,

    /// <summary>
    /// Do not force a version increment; remove the prerelease marker if present.
    /// </summary>
    Stable,

    /// <summary>
    /// Force a minor version increment with respect to the latest stable version; mark the version line as prerelease.
    /// </summary>
    Minor,

    /// <summary>
    /// Force a major version increment and minor version reset with respect to the latest stable version;
    /// mark the version line as prerelease.
    /// </summary>
    Major,
}
