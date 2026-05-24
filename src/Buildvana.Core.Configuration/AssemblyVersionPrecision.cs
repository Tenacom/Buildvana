// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.Configuration;

/// <summary>
/// Specifies how many components of the computed version are carried into the assembly version.
/// </summary>
public enum AssemblyVersionPrecision
{
    /// <summary>Only the major component is significant (<c>major.0.0.0</c>).</summary>
    Major,

    /// <summary>The major and minor components are significant (<c>major.minor.0.0</c>).</summary>
    Minor,

    /// <summary>The major, minor, and build components are significant (<c>major.minor.build.0</c>).</summary>
    Build,

    /// <summary>All four components are significant (<c>major.minor.build.revision</c>).</summary>
    Revision,
}
