// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Sdk.Internal;
using JetBrains.Annotations;
using Microsoft.Build.Framework;

namespace Buildvana.Sdk.Tasks;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class GetWinePaths : BuildvanaSdkTask
{
    public string BasePath { get; set; } = string.Empty;

    public string HostPath1 { get; set; } = string.Empty;

    public string HostPath2 { get; set; } = string.Empty;

    public string HostPath3 { get; set; } = string.Empty;

    public string HostPath4 { get; set; } = string.Empty;

    public string HostPath5 { get; set; } = string.Empty;

    public string HostPath6 { get; set; } = string.Empty;

    public string HostPath7 { get; set; } = string.Empty;

    public string HostPath8 { get; set; } = string.Empty;

    public string HostPath9 { get; set; } = string.Empty;

    public string HostPath10 { get; set; } = string.Empty;

    [Output]
    public string WinePath1 { get; set; } = string.Empty;

    [Output]
    public string WinePath2 { get; set; } = string.Empty;

    [Output]
    public string WinePath3 { get; set; } = string.Empty;

    [Output]
    public string WinePath4 { get; set; } = string.Empty;

    [Output]
    public string WinePath5 { get; set; } = string.Empty;

    [Output]
    public string WinePath6 { get; set; } = string.Empty;

    [Output]
    public string WinePath7 { get; set; } = string.Empty;

    [Output]
    public string WinePath8 { get; set; } = string.Empty;

    [Output]
    public string WinePath9 { get; set; } = string.Empty;

    [Output]
    public string WinePath10 { get; set; } = string.Empty;

    protected override Undefined Run()
    {
        WinePath1 = WinePathUtility.ConvertToWinePath(HostPath1, BasePath);
        WinePath2 = WinePathUtility.ConvertToWinePath(HostPath2, BasePath);
        WinePath3 = WinePathUtility.ConvertToWinePath(HostPath3, BasePath);
        WinePath4 = WinePathUtility.ConvertToWinePath(HostPath4, BasePath);
        WinePath5 = WinePathUtility.ConvertToWinePath(HostPath5, BasePath);
        WinePath6 = WinePathUtility.ConvertToWinePath(HostPath6, BasePath);
        WinePath7 = WinePathUtility.ConvertToWinePath(HostPath7, BasePath);
        WinePath8 = WinePathUtility.ConvertToWinePath(HostPath8, BasePath);
        WinePath9 = WinePathUtility.ConvertToWinePath(HostPath9, BasePath);
        WinePath10 = WinePathUtility.ConvertToWinePath(HostPath10, BasePath);
        return Undefined.Value;
    }
}
