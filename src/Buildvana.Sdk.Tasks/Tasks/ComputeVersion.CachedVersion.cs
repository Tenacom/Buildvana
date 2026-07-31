// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Sdk.Tasks;

partial class ComputeVersion
{
    /// <summary>
    /// The computed version values for a home directory, together with the repository-state fingerprint
    /// they were computed from (<see langword="null"/> if the state could not be fingerprinted).
    /// </summary>
    private sealed record CachedVersion(
        string? Fingerprint,
        string SimpleVersion,
        string SemVer,
        string AssemblyVersion,
        string FileVersion,
        string InformationalVersion,
        bool IsPublicRelease,
        bool IsPrerelease,
        string CommitId,
        int Height);
}
