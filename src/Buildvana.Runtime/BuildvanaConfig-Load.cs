// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text.Json;

namespace Buildvana.Runtime;

public partial record BuildvanaConfig
{
    private const string JsonFileName = "buildvana.json";
    private const string JsoncFileName = "buildvana.jsonc";

    /// <summary>
    /// Finds the configuration file in a home directory, probing the four well-known candidates
    /// (<c>buildvana.json</c>, <c>buildvana.jsonc</c>, and the same names under <c>.buildvana/</c>).
    /// </summary>
    /// <param name="homeDirectory">The home directory to probe; the current directory when omitted.</param>
    /// <returns>The path of the configuration file, or <see langword="null"/> when none exists.</returns>
    /// <exception cref="BuildvanaRuntimeException">More than one configuration file exists.</exception>
    public static string? FindFile(string? homeDirectory = null)
    {
        var baseDirectory = string.IsNullOrEmpty(homeDirectory) ? Directory.GetCurrentDirectory() : homeDirectory;
        string[] candidatePaths =
        [
            Path.Combine(baseDirectory, JsonFileName),
            Path.Combine(baseDirectory, JsoncFileName),
            Path.Combine(baseDirectory, WellKnownPaths.BuildvanaDirectory, JsonFileName),
            Path.Combine(baseDirectory, WellKnownPaths.BuildvanaDirectory, JsoncFileName),
        ];
        var existingPaths = Array.FindAll(candidatePaths, File.Exists);
        if (existingPaths.Length > 1)
        {
            throw new BuildvanaRuntimeException(
                $"Multiple Buildvana configuration files found: {string.Join(", ", existingPaths)}. Keep only one.");
        }

        return existingPaths.Length == 1 ? existingPaths[0] : null;
    }

    /// <summary>
    /// Loads the configuration file found in a home directory.
    /// </summary>
    /// <param name="homeDirectory">The home directory to probe; the current directory when omitted
    /// (hooks run from the home directory).</param>
    /// <returns>The parsed configuration, or an empty <see cref="BuildvanaConfig"/> when no file exists.</returns>
    /// <exception cref="BuildvanaRuntimeException">
    /// More than one configuration file exists, the file cannot be read, or its contents are invalid.
    /// </exception>
    /// <remarks>
    /// <para>This loader does not validate the file beyond deserialization: <c>bv</c> has already validated it,
    /// with schema-based diagnostics, before any hook runs.</para>
    /// </remarks>
    public static BuildvanaConfig Load(string? homeDirectory = null)
    {
        var path = FindFile(homeDirectory);
        if (path is null)
        {
            return new BuildvanaConfig();
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
            return JsonSerializer.Deserialize(json, BuildvanaJsonContext.Default.BuildvanaConfig) ?? new BuildvanaConfig();
        }
        catch (JsonException e)
        {
            throw new BuildvanaRuntimeException($"Invalid configuration file {path}: {e.Message}", e);
        }
    }
}
