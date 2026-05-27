// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Buildvana.Tool.CommandLine;
using Buildvana.Tool.Infrastructure;
using Buildvana.Tool.Services;
using Buildvana.Tool.Services.Solution;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Buildvana.Tool.Subcommands;

/// <summary>
/// Bodies of the individual pipeline steps (clean / restore / build / test / pack), independent of the
/// commands that invoke them. Each step resolves the services it needs from the supplied provider
/// so it remains callable on its own (e.g., from a future <c>bv just &lt;step&gt;</c> subcommand).
/// </summary>
internal static class BuildSteps
{
    /// <summary>
    /// The MSBuild configuration used by the standalone pipeline commands (restore/build/test/pack).
    /// It is emitted as an overridable default, so a user-forwarded <c>-c</c>/<c>-p:Configuration=</c> wins.
    /// </summary>
    public const string DefaultConfiguration = "Release";

    public static Task CleanAsync(IServiceProvider services)
    {
        Guard.IsNotNull(services);
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Clean");
        var solution = services.GetRequiredService<SolutionContext>();

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

        return Task.CompletedTask;
    }

    public static Task RestoreAsync(IServiceProvider services)
    {
        Guard.IsNotNull(services);
        var dotnet = services.GetRequiredService<DotNetService>();
        var solution = services.GetRequiredService<SolutionContext>();
        var forwardedArgs = services.GetRequiredService<CommandParameters>().Forwarded;
        return dotnet.RestoreSolutionAsync(solution, forwardedArgs);
    }

    public static Task BuildAsync(IServiceProvider services, string configuration = DefaultConfiguration)
    {
        Guard.IsNotNull(services);
        var dotnet = services.GetRequiredService<DotNetService>();
        var solution = services.GetRequiredService<SolutionContext>();
        var forwardedArgs = services.GetRequiredService<CommandParameters>().Forwarded;
        return dotnet.BuildSolutionAsync(solution, configuration, forwardedArgs, restore: false);
    }

    public static Task TestAsync(IServiceProvider services, string configuration = DefaultConfiguration)
    {
        Guard.IsNotNull(services);
        var dotnet = services.GetRequiredService<DotNetService>();
        var solution = services.GetRequiredService<SolutionContext>();
        var forwardedArgs = services.GetRequiredService<CommandParameters>().Forwarded;
        return dotnet.TestSolutionAsync(solution, configuration, forwardedArgs, restore: false, build: false);
    }

    public static Task PackAsync(IServiceProvider services, string configuration = DefaultConfiguration)
    {
        Guard.IsNotNull(services);
        var dotnet = services.GetRequiredService<DotNetService>();
        var solution = services.GetRequiredService<SolutionContext>();
        var forwardedArgs = services.GetRequiredService<CommandParameters>().Forwarded;
        return dotnet.PackSolutionAsync(solution, configuration, forwardedArgs, restore: false, build: false);
    }
}
