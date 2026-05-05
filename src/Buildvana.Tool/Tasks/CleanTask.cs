// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;
using Buildvana.Tool.Infrastructure;
using Buildvana.Tool.Services.Solution;
using Buildvana.Tool.Utilities;
using Cake.Frosting;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;

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

        var logger = context.GetService<ILogger<CleanTask>>();
        var solution = context.GetService<SolutionContext>();

        FileSystemHelper.DeleteDirectory(solution.ResolvePath(".vs"), logger);
        FileSystemHelper.DeleteDirectory(solution.ResolvePath("_ReSharper.Caches"), logger);
        FileSystemHelper.DeleteDirectory(solution.ResolvePath("temp"), logger);
        FileSystemHelper.DeleteDirectory(solution.ResolvePath(CommonPaths.AllArtifacts), logger);
        FileSystemHelper.DeleteDirectory(solution.ResolvePath(CommonPaths.TestResults), logger);
        foreach (var project in solution.Model.SolutionProjects)
        {
            var projectDirectory = Path.GetDirectoryName(solution.ResolveProjectPath(project))!;
            FileSystemHelper.DeleteDirectory(Path.Combine(projectDirectory, "bin"), logger);
            FileSystemHelper.DeleteDirectory(Path.Combine(projectDirectory, "obj"), logger);
        }
    }
}
