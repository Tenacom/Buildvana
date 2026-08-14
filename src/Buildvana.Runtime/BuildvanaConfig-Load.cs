// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text.Json;

namespace Buildvana.Runtime;

public partial record BuildvanaConfig
{
    /// <summary>
    /// The name of the configuration file in plain JSON form. The file lives in the home directory itself:
    /// a configuration file elsewhere, <see cref="WellKnownPaths.BuildvanaDirectory"/> included, is not one.
    /// </summary>
    public const string JsonFileName = "buildvana.json";

    /// <summary>
    /// The name of the configuration file in JSON-with-comments form, subject to the same
    /// single candidate location as <see cref="JsonFileName"/>.
    /// </summary>
    public const string JsoncFileName = "buildvana.jsonc";

    /// <summary>
    /// Finds the configuration file in a home directory, probing the two well-known candidates
    /// (<c>buildvana.json</c> and <c>buildvana.jsonc</c>).
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
    public static BuildvanaConfig Load(string? homeDirectory = null) => LoadFile(FindFile(homeDirectory));

    /// <summary>
    /// Loads the configuration file at an already-known path.
    /// </summary>
    /// <param name="path">The path of the configuration file, or <see langword="null"/> for none.</param>
    /// <returns>The parsed configuration, or an empty <see cref="BuildvanaConfig"/> when <paramref name="path"/>
    /// is <see langword="null"/>.</returns>
    /// <exception cref="BuildvanaRuntimeException">
    /// The file cannot be read, or its contents are invalid.
    /// </exception>
    /// <remarks>
    /// <para>A hook reaches this through <see cref="HookArgs.LoadConfig"/>, which passes the path <c>bv</c>
    /// itself read; call it directly only when the path comes from somewhere else.</para>
    /// </remarks>
    public static BuildvanaConfig LoadFile(string? path)
    {
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
