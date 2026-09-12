// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Buildvana.Sdk.Internal;

internal static class WinePathUtility
{
    private static readonly bool IsUnixLikeOS = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public static string ConvertToWinePath(string hostPath, string basePath)
        => ConvertToWinePathCore(hostPath, basePath);

    public static ITaskItem ConvertItem(ITaskItem item, string basePath, string metadataName, string onlyIfMetadata)
    {
        if (!OptionallyFilterByMetadata(item, onlyIfMetadata))
        {
            return new TaskItem(item);
        }

        if (string.IsNullOrEmpty(metadataName))
        {
            return new TaskItem(ConvertToWinePathCore(item.ItemSpec, basePath), item.CloneCustomMetadata());
        }

        foreach (var name in item.MetadataNames)
        {
            if (name is string str && str.Equals(metadataName, StringComparison.OrdinalIgnoreCase))
            {
                var newItem = new TaskItem(item);
                newItem.SetMetadata(str, ConvertToWinePathCore(item.GetMetadata(str), basePath));
                return newItem;
            }
        }

        return new TaskItem(item);
    }

    private static bool OptionallyFilterByMetadata(ITaskItem item, string? onlyIfMetadata)
        => string.IsNullOrEmpty(onlyIfMetadata) || item.GetMetadata(onlyIfMetadata).Equals("true", StringComparison.OrdinalIgnoreCase);

    private static string ConvertToWinePathCore(string hostPath, string basePath)
    {
        if (string.IsNullOrEmpty(hostPath))
        {
            return string.Empty;
        }

        var fullPath = string.IsNullOrEmpty(basePath) ? Path.GetFullPath(hostPath) : Path.GetFullPath(Path.Combine(basePath, hostPath));
        return IsUnixLikeOS ? "Z:" + fullPath.Replace('/', '\\') : fullPath;
    }
}
