// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Infrastructure;
using Buildvana.Tool.Services;
using Buildvana.Tool.Services.Solution;
using Buildvana.Tool.Utilities;
using Cake.Frosting;
using CommunityToolkit.Diagnostics;

using SysPath = System.IO.Path;

namespace Buildvana.Tool.Tasks;

[TaskName(Name)]
[TaskDescription(Description)]
public sealed class CleanTask : FrostingTask<BuildContext>
{
    private const string Name = "Clean";
    private const string Description = "Remove all build artifacts, intermediate output, and temporary files. Like 'dotnet clean', but more aggressive.";

    public override void Run(BuildContext context)
    {
        Guard.IsNotNull(context);

        var paths = context.GetService<PathsService>();
        var solution = context.GetService<SolutionContext>();

        context.DeleteDirectoryIfExists(".vs");
        context.DeleteDirectoryIfExists("_ReSharper.Caches");
        context.DeleteDirectoryIfExists("temp");
        context.DeleteDirectoryIfExists(paths.AllArtifacts);
        context.DeleteDirectoryIfExists(paths.TestResults);
        foreach (var project in solution.Model.SolutionProjects)
        {
            var projectDirectory = SysPath.GetDirectoryName(solution.ResolveProjectPath(project))!;
            context.DeleteDirectoryIfExists(SysPath.Combine(projectDirectory, "bin"));
            context.DeleteDirectoryIfExists(SysPath.Combine(projectDirectory, "obj"));
        }
    }
}
