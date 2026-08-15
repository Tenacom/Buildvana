// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Runtime;

/// <summary>
/// Reads required environment variables.
/// </summary>
internal static class EnvironmentVariables
{
    /// <summary>
    /// Gets the value of an environment variable, requiring it to be present and non-empty.
    /// </summary>
    /// <param name="name">The name of the environment variable.</param>
    /// <returns>The non-empty value of the variable.</returns>
    /// <exception cref="BuildvanaRuntimeException">The variable is missing or empty.</exception>
    public static string GetRequired(string name)
        => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new BuildvanaRuntimeException($"Required environment variable {name} is missing or empty.");
}
