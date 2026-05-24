// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Buildvana.Core;
using Buildvana.Sdk.Internal;
using Buildvana.Sdk.Resources;
using Microsoft.Build.Framework;

namespace Buildvana.Sdk.Tasks;

public sealed class ExportJetBrainsAnnotations : BuildvanaSdkTask
{
    [Required]
    public string AssemblyName { get; set; } = string.Empty;

    [Required]
#pragma warning disable CA1819 // Properties should not return arrays - ITaskItem[] properties of MSBuild tasks are a known exception
    public ITaskItem[] CompileItems { get; set; } = [];
#pragma warning restore CA1819

#pragma warning disable CA1819 // Properties should not return arrays - ITaskItem[] properties of MSBuild tasks are a known exception
    public ITaskItem[] References { get; set; } = [];
#pragma warning restore CA1819

    public string DefineConstants { get; set; } = string.Empty;

    public string LangVersion { get; set; } = string.Empty;

    [Required]
    public string OutputFile { get; set; } = string.Empty;

    protected override Undefined Run()
    {
        try
        {
            var compileFilePaths = CompileItems.Select(GetFullPath).ToList();
            var referencePaths = References.Select(GetFullPath).ToList();
            var preprocessorSymbols = SplitDefineConstants(DefineConstants);
            var document = JetBrainsAnnotationsExporter.Export(AssemblyName, compileFilePaths, referencePaths, preprocessorSymbols, LangVersion);
            document.Save(OutputFile, SaveOptions.None);
        }
        catch (Exception e) when (!e.IsFatalException())
        {
            throw new BuildFailedException(
                string.Format(CultureInfo.InvariantCulture, Strings.JetBrainsAnnotations.ExportFailedFmt, AssemblyName, e.Message),
                e);
        }

        return Undefined.Value;
    }

    private static string GetFullPath(ITaskItem item) => item.GetMetadata("FullPath");

    private static string[] SplitDefineConstants(string defineConstants)
        => defineConstants.Split([';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
