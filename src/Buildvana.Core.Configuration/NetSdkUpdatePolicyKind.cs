// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.Configuration;

/// <summary>
/// Specifies how far an automatic update may move the .NET SDK baseline version pinned in
/// <c>global.json</c>.
/// </summary>
/// <remarks>
/// <para>.NET SDK versions are not SemVer: their patch field encodes <c>featureBand * 100 + patch</c>, so
/// that the feature band of <c>10.0.402</c> is 4 and its patch is 2. "Patch" therefore means something
/// different here, which is why this enum exists alongside <see cref="PackageUpdatePolicyKind"/>. The
/// vocabulary is borrowed from the <c>rollForward</c> setting of <c>global.json</c> and honored on its
/// owner's terms.</para>
/// <para>An <c>Exact</c> kind is deliberately absent. The only move it could name is an RC to the GA of the
/// same version, and <see cref="Patch"/> with <see cref="NetSdkUpdatePolicy.AllowPrerelease"/> set already
/// expresses that move.</para>
/// <para>Members are ordered by how far the kind may move the pin, except for <see cref="Lts"/>, which
/// filters the candidate set instead of narrowing the window and therefore comes last.</para>
/// <para><see cref="Disable"/> is the zero value on purpose: a default-constructed
/// <see cref="NetSdkUpdatePolicy"/> then moves nothing, which is the safe failure.</para>
/// </remarks>
public enum NetSdkUpdatePolicyKind
{
    /// <summary>Do not update; do not list.</summary>
    Disable,

    /// <summary>Update to the latest patch within the same feature band.</summary>
    Patch,

    /// <summary>Update to the latest feature band within the same major and minor.</summary>
    Feature,

    /// <summary>Update to the latest minor within the same major.</summary>
    Minor,

    /// <summary>Update to the latest version.</summary>
    Major,

    /// <summary>Like <see cref="Major"/>, but only long-term support releases qualify.</summary>
    Lts,
}
