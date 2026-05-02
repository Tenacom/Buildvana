// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.HomeDirectory;
using Buildvana.Tool.Services.ServerAdapters;
using Buildvana.Tool.Services.Versioning;
using Cake.Common;
using Cake.Common.IO;
using Cake.Common.Solution;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.MSBuild;
using Cake.Common.Tools.DotNet.NuGet.Push;
using Cake.Common.Tools.DotNet.Test;
using Cake.Core;
using Cake.Core.IO;
using CommunityToolkit.Diagnostics;

using SysDirectory = System.IO.Directory;
using SysPath = System.IO.Path;

namespace Buildvana.Tool.Services;

/// <summary>
/// Provides shortcut methods for .NET SDK operations.
/// </summary>
public sealed class DotNetService
{
    private readonly ICakeContext _context;
    private readonly IBuildHost _host;
    private readonly IHomeDirectoryProvider _home;
    private readonly OptionsService _options;
    private readonly ServerAdapter _server;
    private readonly PathsService _paths;
    private readonly VersionService _version;
    private readonly DotNetMSBuildSettings _msBuildSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="DotNetService"/> class.
    /// </summary>
    public DotNetService(ICakeContext context, IBuildHost host, IHomeDirectoryProvider home, OptionsService options, ServerAdapter server, PathsService paths, VersionService version)
    {
        Guard.IsNotNull(context);
        Guard.IsNotNull(host);
        Guard.IsNotNull(home);
        Guard.IsNotNull(options);
        Guard.IsNotNull(server);
        Guard.IsNotNull(paths);
        Guard.IsNotNull(version);
        _context = context;
        _host = host;
        _home = home;
        _options = options;
        _server = server;
        _paths = paths;
        _version = version;
        _msBuildSettings = new DotNetMSBuildSettings
        {
            MaxCpuCount = 1,
            ContinuousIntegrationBuild = server.IsCloudBuild,
            NoLogo = true,
        };
        SolutionPath = context.GetFiles("*.slnx").FirstOrDefault()
            ?? context.GetFiles("*.sln").FirstOrDefault()
            ?? host.Fail<FilePath>("Cannot find a solution file.");
        Solution = context.ParseSolution(SolutionPath);
        Configuration = context.Argument("configuration", "Release");
        ArtifactsPath = paths.AllArtifacts.Combine(Configuration);
    }

    /// <summary>
    /// Gets the path of the solution file.
    /// </summary>
    public FilePath SolutionPath { get; }

    /// <summary>
    /// Gets the parsed solution.
    /// </summary>
    public SolutionParserResult Solution { get; }

    /// <summary>
    /// Gets the configuration to build.
    /// </summary>
    public string Configuration { get; }

    /// <summary>
    /// Gets the path of the directory where build artifacts for <see cref="Configuration"/> are stored.
    /// </summary>
    public DirectoryPath ArtifactsPath { get; }

    /// <summary>
    /// Restores all NuGet packages for the solution.
    /// </summary>
    public void RestoreSolution()
    {
        _host.LogInformation("Restoring NuGet packages for solution...");
        _context.DotNetRestore(SolutionPath.FullPath, new()
        {
            MSBuildSettings = _msBuildSettings,
            DisableParallel = true,
            Interactive = false,
        });
    }

    /// <summary>
    /// Build all projects in the solution.
    /// </summary>
    /// <param name="restore"><see langword="true"/> to restore NuGet packages before building, <see langword="false"/> otherwise.</param>
    public void BuildSolution(bool restore)
    {
        _host.LogInformation($"Building solution (restore = {restore})...");
        _context.DotNetBuild(SolutionPath.FullPath, new()
        {
            Configuration = Configuration,
            MSBuildSettings = _msBuildSettings,
            NoLogo = true,
            NoRestore = !restore,
        });
    }

    /// <summary>
    /// Run all tests for the solution.
    /// </summary>
    /// <param name="restore"><see langword="true"/> to restore NuGet packages before testing, <see langword="false"/> otherwise.</param>
    /// <param name="build"><see langword="true"/> to build the solution before testing, <see langword="false"/> otherwise.</param>
    public void TestSolution(bool restore, bool build)
    {
        _host.LogInformation("Checking for MTP test projects...");
        var hasTestProjects = false;
        foreach (var project in Solution.Projects.Where(p => !(p is SolutionFolder)))
        {
            _host.LogDebug($"Checking '{project.Path}'...");
            var sb = new StringBuilder();
            _context.DotNetMSBuild(
                project.Path.FullPath,
                new()
                {
                    MaxCpuCount = 1,
                    ContinuousIntegrationBuild = _server.IsCloudBuild,
                    NoLogo = true,
                    ArgumentCustomization = args => args.Append("-getProperty:IsTestingPlatformApplication"),
                },
                output =>
                {
                    foreach (var line in output)
                    {
                        sb.AppendLine(line);
                    }
                });

            if (string.Equals(sb.ToString().Trim(), "true", StringComparison.OrdinalIgnoreCase))
            {
                _host.LogDebug($"Project '{project.Path}' is a test project, will run tests.");
                hasTestProjects = true;
                break;
            }
        }

        if (!hasTestProjects)
        {
            _host.LogInformation("No test projects found, skipping tests.");
            return;
        }

        _host.LogInformation($"Running tests (restore = {restore}, build = {build})...");
        _context.DotNetTest(SolutionPath.FullPath, new()
        {
            PathType = DotNetTestPathType.Solution,
            Configuration = Configuration,

            // Can't use _msBuildSettings here because `dotnet test` passes those to MTP applications,
            // which (at least those built with TUnit) fail because they don't recognize /nologo and /maxCpuCount:1.
            MSBuildSettings = new()
            {
                ContinuousIntegrationBuild = _server.IsCloudBuild,
            },
            NoBuild = !build,
            NoRestore = !restore,
            ArgumentCustomization = args => args
                .Append("--coverage")
                .Append("--coverage-output-format")
                .Append("cobertura")
                .Append("--results-directory")
                .Append(_paths.TestResults.FullPath),
        });
    }

    /// <summary>
    /// Run the Pack target on the solution. This usually produces NuGet packages, but Buildvana SDK may hijack the target to produce, for example, setup executables.
    /// </summary>
    /// <param name="restore"><see langword="true"/> to restore NuGet packages before packing, <see langword="false"/> otherwise.</param>
    /// <param name="build"><see langword="true"/> to build the solution before packing, <see langword="false"/> otherwise.</param>
    public void PackSolution(bool restore, bool build)
    {
        _host.LogInformation($"Packing solution (restore = {restore}, build = {build})...");
        _context.DotNetPack(SolutionPath.FullPath, new()
        {
            Configuration = Configuration,
            MSBuildSettings = _msBuildSettings,
            NoBuild = !build,
            NoLogo = true,
            NoRestore = !restore,
        });
    }

    /// <summary>
    /// Asynchronously pushes all produced NuGet packages to the appropriate NuGet server.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public async Task NuGetPushAllAsync()
    {
        const string nupkgMask = "*.nupkg";
        if (!SysDirectory.EnumerateFiles(ArtifactsPath.FullPath, nupkgMask).Any())
        {
            _host.LogDebug("No .nupkg files to push.");
            return;
        }

        var isPrivate = await _server.IsPrivateRepositoryAsync().ConfigureAwait(false);
        var nugetSource = _options.GetOptionOrFail<string>(isPrivate ? "privateNugetSource" : _version.IsPrerelease ? "prereleaseNugetSource" : "releaseNugetSource");
        var nugetApiKey = _options.GetOptionOrFail<string>(isPrivate ? "privateNugetKey" : _version.IsPrerelease ? "prereleaseNugetKey" : "releaseNugetKey");
        var nugetPushSettings = new DotNetNuGetPushSettings
        {
            ForceEnglishOutput = true,
            Source = nugetSource,
            ApiKey = nugetApiKey,
            SkipDuplicate = true,
        };

        var packages = SysPath.Combine(ArtifactsPath.FullPath, nupkgMask);
        foreach (var path in _context.GetFiles(packages))
        {
            _host.LogInformation($"Pushing {path} to {nugetSource}...");
            _context.DotNetNuGetPush(path, nugetPushSettings);
        }
    }

    private IEnumerable<string> GetCodeCoverageReportPaths()
    {
        var homeDirectory = new DirectoryPath(_home.HomeDirectory);
        foreach (var testResultsDirectory in Solution.Projects.Select(static x => x.Path.GetDirectory().Combine("TestResults")))
        {
            if (!_context.DirectoryExists(testResultsDirectory))
            {
                continue;
            }

            var globPattern = new GlobPattern(string.Join(testResultsDirectory.Separator, testResultsDirectory.FullPath, "**", "coverage.cobertura.xml"));
            foreach (var path in _context.GetFiles(globPattern))
            {
                yield return homeDirectory.GetRelativePath(path).ToString();
            }
        }
    }
}
