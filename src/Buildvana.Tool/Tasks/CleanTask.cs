// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Infrastructure;
using Buildvana.Tool.Services;
using Buildvana.Tool.Utilities;
using Cake.Frosting;
using CommunityToolkit.Diagnostics;

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
        var dotnet = context.GetService<DotNetService>();

        context.DeleteDirectoryIfExists(".vs");
        context.DeleteDirectoryIfExists("_ReSharper.Caches");
        context.DeleteDirectoryIfExists("temp");
        context.DeleteDirectoryIfExists(paths.AllArtifacts);
        context.DeleteDirectoryIfExists(paths.TestResults);
        foreach (var project in dotnet.Solution.Projects)
        {
            var projectDirectory = project.Path.GetDirectory();
            context.DeleteDirectoryIfExists(projectDirectory.Combine("bin"));
            context.DeleteDirectoryIfExists(projectDirectory.Combine("obj"));
        }
    }
}
