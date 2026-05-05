// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Buildvana.Tool.Cli;

/// <summary>
/// Options shared by build-pipeline commands (clean, restore, build, test, pack).
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class BuildSettings : BaseSettings
{
    /// <summary>
    /// Gets the MSBuild configuration to build.
    /// </summary>
    [CommandOption("-c|--configuration <NAME>")]
    [Description("MSBuild configuration to build. Defaults to 'Release' (or the CONFIGURATION environment variable, if set).")]
    public string? Configuration { get; init; }

    /// <summary>
    /// Gets the name of the repository's main branch.
    /// </summary>
    [CommandOption("--mainBranch|--main-branch <NAME>")]
    [Description("Name of the repository's main branch. Defaults to 'main' (or the MAIN_BRANCH environment variable, if set).")]
    public string? MainBranch { get; init; }
}
