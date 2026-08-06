// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using Buildvana.Core;
using Buildvana.Core.Diagnostics;
using Buildvana.Sdk.Resources;
using Microsoft.Build.Framework;

namespace Buildvana.Sdk.Tasks;

/// <summary>
/// Serializes <c>ThisAssemblyConstant</c> items into a constants file
/// consumed by the <c>ThisAssemblyClass</c> source generator.
/// Each line of the file has the form <c>name=value</c>, with both name and value percent-encoded,
/// so that arbitrary characters (including newlines and equal signs) survive the round trip.
/// The file is only rewritten when its content changes, so that up-to-date checks keep working.
/// </summary>
public sealed class WriteThisAssemblyConstantsFile : BuildvanaSdkTask
{
    [Required]
    public string OutputPath { get; set; } = string.Empty;

#pragma warning disable CA1819 // Properties should not return arrays - ITaskItem[] properties of MSBuild tasks are a known exception
    public ITaskItem[] Constants { get; set; } = [];
#pragma warning restore CA1819

    protected override Undefined Run()
    {
        BuildFailedException.ThrowIfNot(
            !string.IsNullOrEmpty(OutputPath),
            string.Format(CultureInfo.InvariantCulture, Strings.MissingParameterFmt, nameof(OutputPath)));

        var sb = new StringBuilder();
        foreach (var item in Constants)
        {
            var name = item.ItemSpec.Trim();
            var value = item.GetMetadata("Value").Trim();
            _ = sb.Append(Uri.EscapeDataString(name))
                  .Append('=')
                  .Append(Uri.EscapeDataString(value))
                  .Append('\n');
        }

        SaveIfDifferent(OutputPath, sb.ToString());
        return Undefined.Value;
    }

    private static void SaveIfDifferent(string outputPath, string content)
    {
        // I/O failures are wrapped inline, not via UserFile, because SDK diagnostics are a documented
        // contract (see docs/SdkDiagnostics.md): every message a task issues must carry a BVSDK code.
        if (ReadCurrentContent(outputPath) == content)
        {
            return;
        }

        try
        {
            File.WriteAllText(outputPath, content);
        }
        catch (Exception e) when (e.IsIORelatedException)
        {
            throw new BuildFailedException(string.Format(CultureInfo.InvariantCulture, Strings.CouldNotWriteFileFmt, outputPath, e.Message), e);
        }
    }

    // Null when the file does not exist, so a first-time write never compares equal.
    private static string? ReadCurrentContent(string outputPath)
    {
        try
        {
            return File.Exists(outputPath) ? File.ReadAllText(outputPath) : null;
        }
        catch (Exception e) when (e.IsIORelatedException)
        {
            throw new BuildFailedException(string.Format(CultureInfo.InvariantCulture, Strings.CouldNotReadFileFmt, outputPath, e.Message), e);
        }
    }
}
