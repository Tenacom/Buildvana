// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Runtime;

/// <summary>
/// Provides extension methods for `GitHubConfig` instances.
/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible — false positive on C# 14 extension blocks; fixed in .NET 11, backport to .NET 10 requested in https://github.com/dotnet/sdk/issues/53984
#pragma warning disable CA1708 // Identifiers should differ by more than case — false positive on classes with C# 14 extension blocks; fixed in .NET 11, https://github.com/dotnet/sdk/issues/51716
public static class GitHubConfigExtensions
{
    extension(GitHubConfig @this)
    {
        /// <summary>
        /// Gets the GitHub access token: the value of the environment variable named by
        /// <see cref="GitHubConfig.TokenEnv"/>, read at the moment of the call.
        /// </summary>
        /// <returns>The token.</returns>
        /// <exception cref="BuildvanaRuntimeException">The environment variable is missing or empty.</exception>
        /// <remarks>
        /// <para>The model stores the variable's name, never its value: the model is serialized into hook args
        /// files, and a resolved secret must never be written to disk. This accessor is a method rather than a
        /// member, so the guarantee is structural — nothing here can be serialized.</para>
        /// </remarks>
        public string GetToken() => EnvironmentVariables.GetRequired(@this.TokenEnv);
    }
}
