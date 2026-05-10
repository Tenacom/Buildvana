// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;

namespace Buildvana.Tool.Configuration;

/// <summary>
/// Typed configuration for NuGet package pushes: one push target per channel (private, prerelease, release).
/// </summary>
/// <remarks>
/// <para>This is the seedling of a future file-backed configuration layer. For now, values are read from
/// environment variables; when the file layer arrives, only <see cref="FromEnvironment"/> changes.</para>
/// </remarks>
internal sealed record NuGetPushConfiguration(
    NuGetPushTarget Private,
    NuGetPushTarget Prerelease,
    NuGetPushTarget Release)
{
    /// <summary>
    /// Reads all push targets from environment variables.
    /// </summary>
    /// <returns>A populated <see cref="NuGetPushConfiguration"/>.</returns>
    /// <exception cref="BuildFailedException">A required environment variable is not set or empty.</exception>
    public static NuGetPushConfiguration FromEnvironment() => new(
        Private: NuGetPushTarget.FromEnvironment("PRIVATE"),
        Prerelease: NuGetPushTarget.FromEnvironment("PRERELEASE"),
        Release: NuGetPushTarget.FromEnvironment("RELEASE"));
}
