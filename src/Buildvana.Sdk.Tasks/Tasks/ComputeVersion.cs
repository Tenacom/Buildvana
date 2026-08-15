// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using Buildvana.Core;
using Buildvana.Sdk.Resources;
using Microsoft.Build.Framework;

namespace Buildvana.Sdk.Tasks;

/// <summary>
/// Computes the version being built from the repository's <c>VERSION</c> file, Git height, and versioning
/// settings, exposing it in the string forms consumed by builds.
/// Results are cached per home directory for the lifetime of the task host process, keyed on the repository
/// state, so that multi-project builds pay for a single Git graph walk.
/// </summary>
public sealed partial class ComputeVersion : BuildvanaSdkTask
{
    [Required]
    public string HomeDirectory { get; set; } = string.Empty;

    [Output]
    public string SimpleVersion { get; private set; } = string.Empty;

    [Output]
    public string SemVer { get; private set; } = string.Empty;

    [Output]
    public string AssemblyVersion { get; private set; } = string.Empty;

    [Output]
    public string FileVersion { get; private set; } = string.Empty;

    [Output]
    public string InformationalVersion { get; private set; } = string.Empty;

    [Output]
    public bool IsPublicRelease { get; private set; }

    [Output]
    public bool IsPrerelease { get; private set; }

    [Output]
    public string CommitId { get; private set; } = string.Empty;

    [Output]
    public int Height { get; private set; }

    protected override Undefined Run()
    {
        BuildFailedException.ThrowIf(
            string.IsNullOrEmpty(HomeDirectory),
            string.Format(CultureInfo.InvariantCulture, Strings.MissingParameterFmt, nameof(HomeDirectory)));

        var version = GetOrComputeVersion(HomeDirectory, Reporter);
        SimpleVersion = version.SimpleVersion;
        SemVer = version.SemVer;
        AssemblyVersion = version.AssemblyVersion;
        FileVersion = version.FileVersion;
        InformationalVersion = version.InformationalVersion;
        IsPublicRelease = version.IsPublicRelease;
        IsPrerelease = version.IsPrerelease;
        CommitId = version.CommitId ?? string.Empty;
        Height = version.Height;
        return Undefined.Value;
    }
}
