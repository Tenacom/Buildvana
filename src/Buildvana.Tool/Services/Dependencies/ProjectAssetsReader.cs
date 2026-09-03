// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Buildvana.Core;
using Buildvana.Core.Diagnostics;
using CommunityToolkit.Diagnostics;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Reads the assets file a restore writes for a project.
/// </summary>
/// <remarks>
/// <para>The file is read through NuGet's own reader, so the format stays NuGet's to change. What comes back
/// is the graph the restore resolved, the packages the project itself references, and the restore's log.</para>
/// <para>Every target graph named here is named in one form, whichever of its two forms the file used. NuGet
/// has written both the short <c>net10.0</c> and the long <c>.NETCoreApp,Version=v10.0</c>, and a caller
/// matching a log entry against a resolved package would otherwise have to try both.</para>
/// </remarks>
internal static partial class ProjectAssetsReader
{
    /// <summary>
    /// Reads a project's assets file.
    /// </summary>
    /// <param name="projectFullPath">The full path of the project the file belongs to.</param>
    /// <param name="assetsFilePath">The full path of the assets file.</param>
    /// <returns>What the file says.</returns>
    /// <exception cref="BuildFailedException">The file is missing, or cannot be read as an assets file.</exception>
    public static ProjectAssets Read(string projectFullPath, string assetsFilePath)
    {
        Guard.IsNotNullOrEmpty(projectFullPath);
        Guard.IsNotNullOrEmpty(assetsFilePath);
        var lockFile = ReadLockFile(projectFullPath, assetsFilePath);
        return new ProjectAssets
        {
            ProjectFullPath = projectFullPath,
            Packages = ReadPackages(lockFile),
            DirectReferences = ReadDirectReferences(lockFile),
            PinsTransitively = lockFile.PackageSpec.RestoreMetadata?.CentralPackageTransitivePinningEnabled ?? false,
            Logs = ReadLogs(lockFile),
        };
    }

    // A restore that fails writes its errors here and its graph too, so a file bv cannot read means the
    // restore never got that far, or wrote something no NuGet reader accepts. Both are the failed step of a
    // program bv invoked, and neither is anything the repository can fix by itself.
    private static LockFile ReadLockFile(string projectFullPath, string assetsFilePath)
    {
        if (!File.Exists(assetsFilePath))
        {
            throw new BuildFailedException(
                ExitCodes.ExternalProgramFailed,
                $"The restore of '{projectFullPath}' wrote no dependency graph: '{assetsFilePath}' does not exist.");
        }

        LockFile lockFile;
        var messages = new MessageCollector();
        try
        {
            lockFile = new LockFileFormat().Read(assetsFilePath, messages);
        }
        catch (Exception e) when (e.IsIORelatedException)
        {
            throw new BuildFailedException(
                ExitCodes.ExternalProgramFailed,
                $"Could not read '{assetsFilePath}': {e.Message}",
                e);
        }

        // NuGet's reader reports a file it cannot parse rather than throwing: it logs the reason and answers
        // with this version, which no assets file states. A project section is there in every file the
        // reader does parse, so its absence says the same thing.
        var isUnreadable = lockFile.Version == int.MinValue || lockFile.PackageSpec is null;
        return isUnreadable
            ? throw new BuildFailedException(
                ExitCodes.ExternalProgramFailed,
                $"Could not read the dependency graph in '{assetsFilePath}'.{messages.Summary}")
            : lockFile;
    }

    private static List<ResolvedPackage> ReadPackages(LockFile lockFile)
    {
        var packages = new List<ResolvedPackage>();
        foreach (var target in lockFile.Targets)
        {
            var targetGraph = NameTargetGraph(target.TargetFramework, target.RuntimeIdentifier);
            foreach (var library in target.Libraries)
            {
                // A target holds the projects of the solution beside the packages, and only a package has a
                // version an override could move.
                if (library is { Type: "package", Name: { } name, Version: { } version })
                {
                    packages.Add(new ResolvedPackage(targetGraph, name, version));
                }
            }
        }

        return packages;
    }

    // What a project references is stated per target framework. A promotion is per project, so the
    // frameworks are merged here.
    private static List<string> ReadDirectReferences(LockFile lockFile)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var framework in lockFile.PackageSpec.TargetFrameworks)
        {
            AddPackageDependencies(ids, framework.Dependencies);
        }

        return [.. ids];
    }

    private static void AddPackageDependencies(HashSet<string> ids, IEnumerable<LibraryDependency> dependencies)
    {
        foreach (var dependency in dependencies)
        {
            if (dependency.LibraryRange.TypeConstraintAllows(LibraryDependencyTarget.Package))
            {
                _ = ids.Add(dependency.Name);
            }
        }
    }

    private static List<AssetsLogEntry> ReadLogs(LockFile lockFile)
    {
        var entries = new List<AssetsLogEntry>();
        foreach (var message in lockFile.LogMessages)
        {
            entries.Add(new AssetsLogEntry(
                message.Code,
                message.Level,
                message.LibraryId ?? string.Empty,
                message.Message ?? string.Empty,
                [.. NameTargetGraphs(message.TargetGraphs)]));
        }

        return entries;
    }

    private static IEnumerable<string> NameTargetGraphs(IReadOnlyList<string>? targetGraphs)
    {
        foreach (var targetGraph in targetGraphs ?? [])
        {
            var separator = targetGraph.IndexOf('/', StringComparison.Ordinal);
            var framework = separator < 0 ? targetGraph : targetGraph[..separator];
            var runtimeIdentifier = separator < 0 ? null : targetGraph[(separator + 1)..];
            yield return NameTargetGraph(NuGetFramework.Parse(framework), runtimeIdentifier);
        }
    }

    private static string NameTargetGraph(NuGetFramework framework, string? runtimeIdentifier)
        => string.IsNullOrEmpty(runtimeIdentifier) ? framework.DotNetFrameworkName : framework.DotNetFrameworkName + "/" + runtimeIdentifier;
}
