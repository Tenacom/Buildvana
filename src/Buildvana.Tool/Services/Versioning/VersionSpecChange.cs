// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Versioning;

/// <summary>
/// Specifies how to modify the version specification upon publishing a release.
/// </summary>
public enum VersionSpecChange
{
    /// <summary>
    /// Do not force a version increment; do not modify the unstable tag.
    /// </summary>
    None,

    /// <summary>
    /// Do not force a version increment; add an unstable tag if not present.
    /// </summary>
    Unstable,

    /// <summary>
    /// Do not force a version increment; remove the unstable tag if present.
    /// </summary>
    Stable,

    /// <summary>
    /// Force a minor version increment with respect to the latest stable version; add an unstable tag.
    /// </summary>
    Minor,

    /// <summary>
    /// Force a major version increment and minor version reset with respect to the latest stable version; add an unstable tag.
    /// </summary>
    Major,
}
