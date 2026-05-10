// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Configuration;

/// <summary>
/// A NuGet push target: source URL and API key.
/// </summary>
internal sealed record NuGetPushTarget(string Source, string ApiKey)
{
    /// <summary>
    /// Reads the source and API key from environment variables named <c>{prefix}_NUGET_SOURCE</c> and <c>{prefix}_NUGET_KEY</c>.
    /// </summary>
    /// <param name="prefix">The environment-variable name prefix (e.g., <c>PRIVATE</c>, <c>PRERELEASE</c>, <c>RELEASE</c>).</param>
    /// <returns>A populated <see cref="NuGetPushTarget"/>.</returns>
    public static NuGetPushTarget FromEnvironment(string prefix) => new(
        Source: ToolConfiguration.RequireEnv($"{prefix}_NUGET_SOURCE"),
        ApiKey: ToolConfiguration.RequireEnv($"{prefix}_NUGET_KEY"));
}
