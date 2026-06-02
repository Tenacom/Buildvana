// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuGet.Versioning;

namespace Buildvana.Core.Versioning;

/// <summary>
/// Represents the result of a version computation performed by <see cref="VersionCalculator.Compute"/>.
/// </summary>
/// <param name="SemanticVersion">The computed semantic version.</param>
/// <param name="CurrentStr">The normalized SemVer 2.0 string form of <paramref name="SemanticVersion"/>.</param>
/// <param name="IsPublicRelease">A value indicating whether the build is a public release, i.e. the current branch
/// matches one of the configured public-release patterns.</param>
/// <param name="IsPrerelease">A value indicating whether the computed version is a prerelease.</param>
public sealed record CalculatedVersion(
    SemanticVersion SemanticVersion,
    string CurrentStr,
    bool IsPublicRelease,
    bool IsPrerelease);
