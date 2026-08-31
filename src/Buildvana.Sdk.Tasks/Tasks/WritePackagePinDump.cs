// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Dependencies;
using Buildvana.Core.Diagnostics;
using Buildvana.Sdk.Resources;
using Microsoft.Build.Framework;

namespace Buildvana.Sdk.Tasks;

/// <summary>
/// Writes the package items of one evaluation to a JSON file, for <c>bv dependencies</c> to read.
/// </summary>
/// <remarks>
/// <para>One file is written per evaluation, named after the project and the target framework, so that
/// projects built in parallel never write the same file. <c>bv</c> reads every file of the directory it
/// named, so the file name is an implementation detail of the pair.</para>
/// <para>The task judges nothing: it writes the items it is given, metadata and all, and leaves to
/// <c>bv</c> the decisions about which of them are the repository's to manage.</para>
/// </remarks>
public sealed class WritePackagePinDump : BuildvanaSdkTask
{
    [Required]
    public string OutputDirectory { get; set; } = string.Empty;

    [Required]
    public string ProjectFullPath { get; set; } = string.Empty;

    public string TargetFramework { get; set; } = string.Empty;

    public bool ManagePackageVersionsCentrally { get; set; }

#pragma warning disable CA1819 // Properties should not return arrays - ITaskItem[] properties of MSBuild tasks are a known exception
    public ITaskItem[] PackageVersions { get; set; } = [];

    public ITaskItem[] GlobalPackageReferences { get; set; } = [];

    public ITaskItem[] PackageReferences { get; set; } = [];
#pragma warning restore CA1819

    protected override Undefined Run()
    {
        ThrowIfMissing(OutputDirectory, nameof(OutputDirectory));
        ThrowIfMissing(ProjectFullPath, nameof(ProjectFullPath));

        var items = new List<PackagePinDumpItem>();
        AddItems(items, "PackageVersion", PackageVersions);
        AddItems(items, "GlobalPackageReference", GlobalPackageReferences);
        AddItems(items, "PackageReference", PackageReferences);
        var dump = new PackagePinDump
        {
            ProjectFullPath = ProjectFullPath,
            TargetFramework = NullIfEmpty(TargetFramework),
            ManagePackageVersionsCentrally = ManagePackageVersionsCentrally,
            Items = items,
        };

        var path = Path.Combine(OutputDirectory, GetFileName(ProjectFullPath, TargetFramework));
        Write(path, JsonSerializer.Serialize(dump, PackagePinDumpJsonContext.Default.PackagePinDump));
        Reporter.Detail($"Package pins of {ProjectFullPath} written to {path}.");
        return Undefined.Value;
    }

    private static void ThrowIfMissing(string value, string parameterName)
        => BuildFailedException.ThrowIf(
            string.IsNullOrEmpty(value),
            string.Format(CultureInfo.InvariantCulture, Strings.MissingParameterFmt, parameterName));

    private static void AddItems(List<PackagePinDumpItem> items, string itemType, ITaskItem[] taskItems)
    {
        foreach (var taskItem in taskItems)
        {
            items.Add(new PackagePinDumpItem
            {
                ItemType = itemType,
                Id = taskItem.ItemSpec,
                Version = NullIfEmpty(taskItem.GetMetadata("Version")),
                VersionOverride = NullIfEmpty(taskItem.GetMetadata("VersionOverride")),
                UpdatePolicy = NullIfEmpty(taskItem.GetMetadata("UpdatePolicy")),
                IsImplicitlyDefined = string.Equals(
                    taskItem.GetMetadata("IsImplicitlyDefined"),
                    "true",
                    StringComparison.OrdinalIgnoreCase),

                // The target copies MSBuild's own DefiningProjectFullPath modifier into this metadatum,
                // because a modifier is computed from an item's context and does not travel as a value.
                DefiningProjectFullPath = taskItem.GetMetadata("BV_DefiningProjectFullPath"),
            });
        }
    }

    // MSBuild has no absent metadatum: one that was never stated reads as an empty string.
    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    // The project name and the target framework make the file recognizable at a glance; the hash of the two
    // makes it unique, since a solution may well build two projects of the same name from two directories.
    private static string GetFileName(string projectFullPath, string targetFramework)
    {
        var key = projectFullPath.ToUpperInvariant() + "|" + targetFramework;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
#pragma warning disable CA1308 // Normalize strings to uppercase - a file name is not a comparison normalization
        var name = Path.GetFileNameWithoutExtension(projectFullPath).ToLowerInvariant();
#pragma warning restore CA1308
        var framework = targetFramework.Length == 0 ? "all" : targetFramework;
        return $"{name}-{framework}-{Convert.ToHexStringLower(hash)[..16]}.json";
    }

    private static void Write(string path, string content)
    {
        // I/O failures are wrapped inline, not via UserFile, because SDK diagnostics are a documented
        // contract (see docs/SdkDiagnostics.md): every message a task issues must carry a BVSDK code.
        try
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
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
