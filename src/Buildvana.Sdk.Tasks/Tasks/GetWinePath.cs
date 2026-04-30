// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Sdk.Internal;
using Buildvana.Sdk.Resources;
using Microsoft.Build.Framework;

namespace Buildvana.Sdk.Tasks;

public sealed class GetWinePath : BuildvanaSdkTask
{
    public string BasePath { get; set; } = string.Empty;

    [Required]
    public string HostPath { get; set; } = string.Empty;

    [Output]
    public string WinePath { get; set; } = string.Empty;

    protected override Undefined Run()
    {
        Host.Ensure(!string.IsNullOrEmpty(HostPath), Strings.MissingParameterFmt, nameof(HostPath));

        WinePath = WinePathUtility.ConvertToWinePath(HostPath, BasePath);
        return Undefined.Value;
    }
}
