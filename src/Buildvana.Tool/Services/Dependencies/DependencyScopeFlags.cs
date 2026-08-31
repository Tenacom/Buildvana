// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Turns the scope flags of a command line into scopes.
/// </summary>
/// <remarks>
/// <para>Every subcommand of <c>bv dependencies</c> declares the same eight flags, because the help renderer
/// and the argument validator read the flags a settings type declares itself. What the flags mean is stated
/// once, here.</para>
/// </remarks>
internal static class DependencyScopeFlags
{
    /// <summary>
    /// Names the scopes whose flags are set, in scope order.
    /// </summary>
    /// <param name="netSdk">Whether the .NET SDK flag is set.</param>
    /// <param name="sdks">Whether the project SDK flag is set.</param>
    /// <param name="tools">Whether the tools flag is set.</param>
    /// <param name="packages">Whether the packages flag is set.</param>
    /// <returns>The named scopes.</returns>
    public static IReadOnlyList<DependencyScope> Of(bool netSdk, bool sdks, bool tools, bool packages)
    {
        var scopes = new List<DependencyScope>();
        if (netSdk)
        {
            scopes.Add(DependencyScope.NetSdk);
        }

        if (sdks)
        {
            scopes.Add(DependencyScope.Sdks);
        }

        if (tools)
        {
            scopes.Add(DependencyScope.Tools);
        }

        if (packages)
        {
            scopes.Add(DependencyScope.Packages);
        }

        return scopes;
    }
}
