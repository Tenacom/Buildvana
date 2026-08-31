// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// The names of the dependency scopes, as the configuration file states them and the command line names
/// them.
/// </summary>
internal static class DependencyScopeNames
{
    /// <summary>The name of the <see cref="DependencyScope.NetSdk"/> scope.</summary>
    public const string NetSdk = "netsdk";

    /// <summary>The name of the <see cref="DependencyScope.Sdks"/> scope.</summary>
    public const string Sdks = "sdks";

    /// <summary>The name of the <see cref="DependencyScope.Tools"/> scope.</summary>
    public const string Tools = "tools";

    /// <summary>The name of the <see cref="DependencyScope.Packages"/> scope.</summary>
    public const string Packages = "packages";

    /// <summary>
    /// Gets the name of a scope.
    /// </summary>
    /// <param name="scope">The scope.</param>
    /// <returns>The scope's name.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="scope"/> is not a scope.</exception>
    public static string Of(DependencyScope scope)
        => scope switch
        {
            DependencyScope.NetSdk => NetSdk,
            DependencyScope.Sdks => Sdks,
            DependencyScope.Tools => Tools,
            DependencyScope.Packages => Packages,
            _ => throw new ArgumentOutOfRangeException(nameof(scope)),
        };
}
