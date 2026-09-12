// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Buildvana.Sdk.Internal;
using Microsoft.Build.Framework;

namespace Buildvana.Sdk.Tasks;

public sealed class ConvertToWinePaths : BuildvanaSdkTask
{
    private readonly List<ITaskItem> _convertedItems = [];

    public string BasePath { get; set; } = string.Empty;

    [Required]
#pragma warning disable CA1819 // Properties should not return arrays - ITaskItem[] properties of MSBuild tasks are a known exception
    public ITaskItem[] Items { get; set; } = [];
#pragma warning restore CA1819 // Properties should not return arrays

    public string MetadataName { get; set; } = string.Empty;

    public string OnlyIfMetadata { get; set; } = string.Empty;

    [Output]
#pragma warning disable CA1819 // Properties should not return arrays - ITaskItem[] properties of MSBuild tasks are a known exception
    public ITaskItem[] ConvertedItems => [.. _convertedItems];
#pragma warning restore CA1819 // Properties should not return arrays

    protected override Undefined Run()
    {
        _convertedItems.Clear();
        _convertedItems.AddRange(Items.Select(x => WinePathUtility.ConvertItem(x, BasePath, MetadataName, OnlyIfMetadata)));
        return Undefined.Value;
    }
}
