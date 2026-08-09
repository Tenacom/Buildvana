// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// The base type for hook args: the properties shared by the args of every hook, plus the loading
/// plumbing shared by the <c>Load</c> methods of concrete args types.
/// </summary>
/// <remarks>
/// <para>Every hook's args type derives from this record and implements <see cref="IHookEvent"/>,
/// naming the hook it belongs to.</para>
/// </remarks>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public abstract record HookArgs
{
    /// <summary>
    /// Gets run-time information about the bv run: the running version, how the run was launched, and the
    /// absolute paths of the run's well-known directories.
    /// </summary>
    /// <remarks>
    /// <para>Serialized first in every args file, before the properties of the concrete args type,
    /// for the benefit of humans inspecting the file.</para>
    /// </remarks>
    [JsonPropertyOrder(-1)]
    public required RuntimeInfo RuntimeInfo { get; init; }

    /// <summary>
    /// Loads the args of the current run of the hook identified by <typeparamref name="TArgs"/>
    /// from the hook's args file.
    /// </summary>
    /// <typeparam name="TArgs">The type of the hook's args, identifying the hook.</typeparam>
    /// <param name="jsonTypeInfo">The source-generated serialization metadata for <typeparamref name="TArgs"/>.</param>
    /// <param name="homeDirectory">The home directory the args file path is resolved against;
    /// the current directory when omitted (hooks run from the home directory).</param>
    /// <returns>The deserialized hook args.</returns>
    /// <exception cref="BuildvanaRuntimeException">
    /// The args file does not exist (<c>bv</c> has never run this hook in this repository, or the current
    /// directory is not the home directory), cannot be read, or does not contain valid args.
    /// </exception>
    protected static TArgs Load<TArgs>(JsonTypeInfo<TArgs> jsonTypeInfo, string? homeDirectory)
        where TArgs : HookArgs, IHookEvent
    {
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);
        var relativePath = WellKnownPaths.GetHookArgsFile(TArgs.Context, TArgs.Event);
        homeDirectory = string.IsNullOrEmpty(homeDirectory) ? Environment.CurrentDirectory : homeDirectory;
        var path = Path.Combine(homeDirectory, relativePath);
        if (!File.Exists(path))
        {
            throw new BuildvanaRuntimeException($"The hook args file '{path}' does not exist.");
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception e) when (e.IsIORelatedException)
        {
            throw new BuildvanaRuntimeException($"Could not read from {path}: {e.Message}", e);
        }

        try
        {
            return JsonSerializer.Deserialize(json, jsonTypeInfo)
                ?? throw new BuildvanaRuntimeException($"The hook args file '{path}' does not contain a JSON object.");
        }
        catch (JsonException e)
        {
            throw new BuildvanaRuntimeException($"Invalid hook args file {path}: {e.Message}", e);
        }
    }
}
