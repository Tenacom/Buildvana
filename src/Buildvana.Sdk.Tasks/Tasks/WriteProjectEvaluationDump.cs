// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Diagnostics;
using Buildvana.Core.ProjectEvaluation;
using Buildvana.Sdk.Resources;
using Microsoft.Build.Framework;

namespace Buildvana.Sdk.Tasks;

/// <summary>
/// Writes the evaluated facts of one project to a JSON file, for <c>bv</c> to describe the project to hooks.
/// </summary>
/// <remarks>
/// <para>One file is written per project, named after the project, so that projects built in parallel never
/// write the same file. <c>bv</c> reads every file of the directory it named, so the file name is an
/// implementation detail of the pair.</para>
/// <para>The task judges nothing: it writes the values it is given, and leaves to the hooks the decision
/// of what a combination of them means.</para>
/// </remarks>
public sealed class WriteProjectEvaluationDump : BuildvanaSdkTask
{
    [Required]
    public string OutputDirectory { get; set; } = string.Empty;

    [Required]
    public string ProjectFullPath { get; set; } = string.Empty;

    public bool IsPackable { get; set; }

    public bool PackAsTool { get; set; }

    public string PackageId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets one item per target framework, whose identity is the target framework.
    /// </summary>
    /// <remarks>
    /// <para>Each item carries the <c>TargetPath</c>, <c>DocumentationFile</c>, <c>OutputType</c>, and
    /// <c>IsTestProject</c> metadata, as the evaluation for its target framework states them.</para>
    /// </remarks>
#pragma warning disable CA1819 // Properties should not return arrays - ITaskItem[] properties of MSBuild tasks are a known exception
    public ITaskItem[] TargetFrameworks { get; set; } = [];
#pragma warning restore CA1819

    protected override Undefined Run()
    {
        ThrowIfMissing(OutputDirectory, nameof(OutputDirectory));
        ThrowIfMissing(ProjectFullPath, nameof(ProjectFullPath));

        var dump = new ProjectEvaluationDump {
            ProjectFullPath = ProjectFullPath,
            IsPackable = IsPackable,
            PackAsTool = PackAsTool,
            PackageId = NullIfEmpty(PackageId),
            TargetFrameworks = GetTargetFrameworks(TargetFrameworks),
        };

        var path = Path.Combine(OutputDirectory, GetFileName(ProjectFullPath));
        Write(path, JsonSerializer.Serialize(dump, ProjectEvaluationDumpJsonContext.Default.ProjectEvaluationDump));
        Reporter.Detail($"Evaluation of {ProjectFullPath} written to {path}.");
        return Undefined.Value;
    }

    private static void ThrowIfMissing(string value, string parameterName)
        => BuildFailedException.ThrowIf(
            string.IsNullOrEmpty(value),
            string.Format(CultureInfo.InvariantCulture, Strings.MissingParameterFmt, parameterName));

    private static List<ProjectEvaluationDumpTargetFramework> GetTargetFrameworks(ITaskItem[] taskItems)
    {
        var targetFrameworks = new List<ProjectEvaluationDumpTargetFramework>();
        foreach (var taskItem in taskItems)
        {
            targetFrameworks.Add(new ProjectEvaluationDumpTargetFramework {
                TargetFramework = taskItem.ItemSpec,
                TargetPath = taskItem.GetMetadata("TargetPath"),
                DocumentationFile = NullIfEmpty(taskItem.GetMetadata("DocumentationFile")),
                OutputType = NullIfEmpty(taskItem.GetMetadata("OutputType")),
                IsTestProject = string.Equals(taskItem.GetMetadata("IsTestProject"), "true", StringComparison.OrdinalIgnoreCase),
            });
        }

        return targetFrameworks;
    }

    // MSBuild has no absent value: a property or metadatum that was never stated reads as an empty string.
    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    // The project name makes the file recognizable at a glance; the hash of the project path makes it unique,
    // since a solution may well build two projects of the same name from two directories.
    private static string GetFileName(string projectFullPath)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(projectFullPath.ToUpperInvariant()));
#pragma warning disable CA1308 // Normalize strings to uppercase - a file name is not a comparison normalization
        var name = Path.GetFileNameWithoutExtension(projectFullPath).ToLowerInvariant();
#pragma warning restore CA1308
        return $"{name}-{Convert.ToHexStringLower(hash)[..16]}.json";
    }

    private static void Write(string path, string content)
    {
        // I/O failures are wrapped inline, not via UserFile, because SDK diagnostics are a documented
        // contract (see docs/sdk-diagnostics.md): every message a task issues must carry a BVSDK code.
        try
        {
            var directory = Path.GetDirectoryName(path);
            Debug.Assert(directory is not null, "GetDirectoryName returns null only for a root, and a file path is not one.");
            _ = Directory.CreateDirectory(directory);
            File.WriteAllText(path, content);
        }
        catch (Exception e) when (e.IsIORelatedException)
        {
            throw new BuildFailedException(
                string.Format(CultureInfo.InvariantCulture, Strings.CouldNotWriteFileFmt, path, e.Message),
                e);
        }
    }
}
