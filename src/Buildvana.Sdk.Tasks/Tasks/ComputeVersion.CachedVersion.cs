// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Versioning;

namespace Buildvana.Sdk.Tasks;

partial class ComputeVersion
{
    /// <summary>
    /// The version computed for a home directory, together with the repository-state fingerprint it was
    /// computed from (<see langword="null"/> if the state could not be fingerprinted).
    /// </summary>
    private sealed record CachedVersion(string? Fingerprint, VersionInfo Version);
}
