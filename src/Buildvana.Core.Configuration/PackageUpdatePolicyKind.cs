// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.Configuration;

/// <summary>
/// Specifies how far an automatic update may move the pin of a package, a .NET tool, or an MSBuild project
/// SDK.
/// </summary>
/// <remarks>
/// <para>Members are ordered by how far the kind may move a pin, so that ordinal order carries meaning.</para>
/// <para><see cref="Disable"/> is the zero value on purpose: a default-constructed
/// <see cref="PackageUpdatePolicy"/> then moves nothing, which is the safe failure.</para>
/// </remarks>
public enum PackageUpdatePolicyKind
{
    /// <summary>Do not update. A single pin stays listed; a whole scope is skipped entirely.</summary>
    Disable,

    /// <summary>Update within the same major, minor, and patch (e.g. <c>1.2.0-preview.1</c> to <c>1.2.0</c>).</summary>
    Exact,

    /// <summary>Update to the latest patch within the same major and minor.</summary>
    Patch,

    /// <summary>Update to the latest minor or patch within the same major.</summary>
    Minor,

    /// <summary>Update to the latest version.</summary>
    Major,
}
