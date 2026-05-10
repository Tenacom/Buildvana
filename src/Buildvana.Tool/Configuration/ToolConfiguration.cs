// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using Buildvana.Core;

namespace Buildvana.Tool.Configuration;

/// <summary>
/// Typed configuration for <c>bv</c>: secrets and endpoints with no CLI-flag counterpart.
/// </summary>
/// <remarks>
/// <para>This is the seedling of a future file-backed configuration layer. For now, values are read from
/// environment variables; when the file layer arrives, only <see cref="FromEnvironment"/> changes.</para>
/// </remarks>
internal sealed record ToolConfiguration(
    string GitHubToken,
    NuGetPushTarget PrivateNuGet,
    NuGetPushTarget PrereleaseNuGet,
    NuGetPushTarget ReleaseNuGet)
{
    /// <summary>
    /// Reads all configuration values from environment variables.
    /// </summary>
    /// <returns>A populated <see cref="ToolConfiguration"/>.</returns>
    /// <exception cref="BuildFailedException">A required environment variable is not set or empty.</exception>
    public static ToolConfiguration FromEnvironment() => new(
        GitHubToken: RequireEnv("GITHUB_TOKEN"),
        PrivateNuGet: NuGetPushTarget.FromEnvironment("PRIVATE"),
        PrereleaseNuGet: NuGetPushTarget.FromEnvironment("PRERELEASE"),
        ReleaseNuGet: NuGetPushTarget.FromEnvironment("RELEASE"));

    internal static string RequireEnv(string name)
        => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v
            ? v
            : throw new BuildFailedException($"Required environment variable {name} is not set or empty.");
}
