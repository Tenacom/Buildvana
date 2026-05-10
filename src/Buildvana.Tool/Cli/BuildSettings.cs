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
    [Description("MSBuild configuration to build. Defaults to 'Release'.")]
    public string? Configuration { get; init; }

    /// <summary>
    /// Gets the name of the repository's main branch.
    /// </summary>
    [CommandOption("--main-branch <NAME>")]
    [Description("Name of the repository's main branch. Defaults to 'main'.")]
    public string? MainBranch { get; init; }

    /// <summary>
    /// Gets the resolved MSBuild configuration: <see cref="Configuration"/> if set, otherwise <c>"Release"</c>.
    /// </summary>
    public string ResolveConfiguration() => Configuration ?? "Release";

    /// <summary>
    /// Gets the configured main branch name, or the empty string if none was passed (in which case
    /// <c>GitService</c> auto-detects from <c>main</c>/<c>master</c>).
    /// </summary>
    public string ResolveMainBranch() => MainBranch ?? string.Empty;
}
