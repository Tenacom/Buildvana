// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Runtime;

/// <summary>
/// Provides extension methods for `NuGetFeedConfig` instances.
/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible — false positive on C# 14 extension blocks; fixed in .NET 11, backport to .NET 10 requested in https://github.com/dotnet/sdk/issues/53984
#pragma warning disable CA1708 // Identifiers should differ by more than case — false positive on classes with C# 14 extension blocks; fixed in .NET 11, https://github.com/dotnet/sdk/issues/51716
public static class NuGetFeedConfigExtensions
{
    extension(NuGetFeedConfig @this)
    {
        /// <summary>
        /// Gets the feed API key: the value of the environment variable named by
        /// <see cref="NuGetFeedConfig.ApiKeyEnv"/>, read at the moment of the call.
        /// </summary>
        /// <returns>The API key.</returns>
        /// <exception cref="BuildvanaRuntimeException">The environment variable is missing or empty.</exception>
        /// <remarks>
        /// <para>The model stores the variable's name, never its value: the model is serialized into hook args
        /// files, and a resolved secret must never be written to disk. This accessor is a method rather than a
        /// member, so the guarantee is structural — nothing here can be serialized.</para>
        /// </remarks>
        public string GetApiKey() => EnvironmentVariables.GetRequired(@this.ApiKeyEnv);
    }
}
