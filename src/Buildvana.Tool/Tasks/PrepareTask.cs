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
public sealed class PrepareTask : FrostingTask<BuildContext>
{
    private const string Name = "Prepare";
    private const string Description = "Prepare";

    public override void Run(BuildContext context)
    {
        Guard.IsNotNull(context);

        var dotnet = context.GetService<DotNetService>();
        context.DeleteDirectoryIfExists(".vs");
        context.DeleteDirectoryIfExists("_ReSharper.Caches");
        context.DeleteDirectoryIfExists("artifacts");
        context.DeleteDirectoryIfExists("temp");
        foreach (var project in dotnet.Solution.Projects)
        {
            var projectDirectory = project.Path.GetDirectory();
            context.DeleteDirectoryIfExists(projectDirectory.Combine("bin"));
            context.DeleteDirectoryIfExists(projectDirectory.Combine("obj"));
            context.DeleteDirectoryIfExists(projectDirectory.Combine("TestResults"));
        }
    }
}
