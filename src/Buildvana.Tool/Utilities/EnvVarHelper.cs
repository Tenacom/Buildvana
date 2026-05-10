// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using Buildvana.Core;

namespace Buildvana.Tool.Utilities;

/// <summary>
/// Provides helper methods for accessing environment variables.
/// </summary>
public static class EnvVarHelper
{
    /// <summary>
    /// Returns the value of the named environment variable, or fails the build if it is not set or empty.
    /// </summary>
    /// <param name="name">The environment variable name.</param>
    /// <returns>The non-empty value of the environment variable.</returns>
    /// <exception cref="BuildFailedException">The environment variable is not set or empty.</exception>
    public static string Require(string name)
        => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v
            ? v
            : throw new BuildFailedException($"Required environment variable {name} is not set or empty.");
}
