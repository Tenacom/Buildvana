// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.ObjectModel;
using Microsoft.Build.Framework;

/// <summary>
/// A <see cref="RecordingBuildEngine"/> that also implements <see cref="IBuildEngine10"/>, so that
/// <see cref="IBuildEngine10.EngineServices"/> (and with it importance-based filtering) is available.
/// Members that are irrelevant to the tests are benign no-ops; project-building members throw.
/// </summary>
internal sealed class RecordingBuildEngine10 : RecordingBuildEngine, IBuildEngine10
{
    public bool IsRunningMultipleNodes => false;

    public bool AllowFailureWithoutError { get; set; }

    public EngineServices EngineServices { get; init; } = new StubEngineServices();

    public bool BuildProjectFile(
        string? projectFileName,
        string[]? targetNames,
        IDictionary? globalProperties,
        IDictionary? targetOutputs,
        string? toolsVersion)
        => throw new NotSupportedException();

    public bool BuildProjectFilesInParallel(
        string[]? projectFileNames,
        string[]? targetNames,
        IDictionary[]? globalProperties,
        IDictionary[]? targetOutputsPerProject,
        string[]? toolsVersion,
        bool useResultsCache,
        bool unloadProjectsOnCompletion)
        => throw new NotSupportedException();

    public BuildEngineResult BuildProjectFilesInParallel(
        string[]? projectFileNames,
        string[]? targetNames,
        IDictionary[]? globalProperties,
        IList<string>[]? removeGlobalProperties,
        string[]? toolsVersion,
        bool returnTargetOutputs)
        => throw new NotSupportedException();

    public void Yield()
    {
    }

    public void Reacquire()
    {
    }

    public void RegisterTaskObject(
        object? key,
        object? obj,
        RegisteredTaskObjectLifetime lifetime,
        bool allowEarlyCollection)
    {
    }

    public object? GetRegisteredTaskObject(object? key, RegisteredTaskObjectLifetime lifetime) => null;

    public object? UnregisterTaskObject(object? key, RegisteredTaskObjectLifetime lifetime) => null;

    public void LogTelemetry(string? eventName, IDictionary<string, string>? properties)
    {
    }

    public IReadOnlyDictionary<string, string> GetGlobalProperties() => ReadOnlyDictionary<string, string>.Empty;

    public bool ShouldTreatWarningAsError(string? warningCode) => false;

    public int RequestCores(int requestedCores) => requestedCores;

    public void ReleaseCores(int coresToRelease)
    {
    }
}
