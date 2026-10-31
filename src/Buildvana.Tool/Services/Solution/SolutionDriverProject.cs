// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Solution;

/// <summary>
/// Builds the project <c>bv</c> hands to MSBuild in order to run a target of the Buildvana SDK on every project
/// of a solution: a project of its own that asks each of the solution's projects for the target.
/// </summary>
/// <remarks>
/// <para>Running the target on the solution file itself would be shorter, and it would fail: MSBuild
/// demands that every project of a solution define the target it is asked for, and a solution may well
/// hold a project the Buildvana SDK never sees. The driver asks for the target with
/// <c>SkipNonexistentTargets</c>, so such a project contributes nothing instead of ending the run.</para>
/// <para>One invocation builds them all, in parallel, in one process. The alternative — one
/// <c>dotnet msbuild</c> per project — pays the cost of loading MSBuild and the .NET SDK once per project,
/// and would still need a second invocation per target framework of a multi-targeting one.</para>
/// </remarks>
internal static class SolutionDriverProject
{
    /// <summary>
    /// The name of the target <c>bv</c> asks the driver project for.
    /// </summary>
    public const string TargetName = "BV_RunSolutionTarget";

    // The characters MSBuild reads as syntax rather than as text. A path may hold any of them: parentheses
    // are ordinary in `Program Files (x86)`, and a semicolon would otherwise split one path into two items.
    private const string SpecialCharacters = "%$@;*?'()";

    /// <summary>
    /// Builds the driver project for the given projects.
    /// </summary>
    /// <param name="projectPaths">The full paths of the projects to run the target on.</param>
    /// <param name="projectTargetName">The name of the target to ask each project for, which the Buildvana SDK
    /// defines.</param>
    /// <returns>The text of the driver project.</returns>
    public static string Create(IReadOnlyList<string> projectPaths, string projectTargetName)
    {
        Guard.IsNotNull(projectPaths);
        Guard.IsNotNullOrEmpty(projectTargetName);
        var items = string.Join(
            "\n",
            projectPaths.Select(static path => $"""    <BV_SolutionProject Include="{Escape(path)}" />"""));

        return $"""
                <Project>

                  <ItemGroup>
                {items}
                  </ItemGroup>

                  <Target Name="{TargetName}">
                    <MSBuild Projects="@(BV_SolutionProject)"
                             Targets="{projectTargetName}"
                             BuildInParallel="true"
                             SkipNonexistentTargets="true" />
                  </Target>

                </Project>

                """;
    }

    // A path goes into the driver project through two layers, and each has its own escaping: MSBuild's,
    // which turns a syntax character into a %XX sequence it reads back as text, and XML's.
    private static string Escape(string path)
    {
        var builder = new StringBuilder(path.Length);
        foreach (var c in path)
        {
            _ = SpecialCharacters.Contains(c, StringComparison.Ordinal)
                ? builder.Append('%').Append(((int)c).ToString("X2", CultureInfo.InvariantCulture))
                : builder.Append(c);
        }

        return SecurityElement.Escape(builder.ToString());
    }
}
