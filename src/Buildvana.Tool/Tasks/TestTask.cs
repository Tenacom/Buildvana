// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Buildvana.Tool.Infrastructure;
using Buildvana.Tool.Services;
using Buildvana.Tool.Services.Solution;
using Cake.Frosting;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Tasks;

[TaskName(Name)]
[TaskDescription(Description)]
[IsDependentOn(typeof(BuildTask))]
public sealed class TestTask : AsyncFrostingTask<BuildContext>
{
    private const string Name = "Test";
    private const string Description = "Build all projects and run tests";

    public override Task RunAsync(BuildContext context)
    {
        Guard.IsNotNull(context);

        var dotnet = context.GetService<DotNetService>();
        var solution = context.GetService<SolutionContext>();
        return dotnet.TestSolutionAsync(solution, false, false);
    }
}
