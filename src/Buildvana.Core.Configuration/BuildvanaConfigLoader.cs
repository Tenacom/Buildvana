// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Loads and validates the Buildvana configuration file found in a home directory.
/// </summary>
public static class BuildvanaConfigLoader
{
    private const string JsonFileName = "buildvana.json";
    private const string JsoncFileName = "buildvana.jsonc";

    private static readonly string[] AllowedDotNetArgsKeys = ["all", "restore", "build", "test", "pack"];
    private static readonly string[] AllowedNuGetFeedKeys = ["prerelease", "release"];

    /// <summary>
    /// Loads the configuration file found in <paramref name="homeDirectory"/>.
    /// </summary>
    /// <param name="homeDirectory">The home directory to search for a configuration file.</param>
    /// <returns>The parsed configuration, or an empty <see cref="BuildvanaConfig"/> if no file is present.</returns>
    /// <exception cref="BuildFailedException">
    /// Both <c>buildvana.json</c> and <c>buildvana.jsonc</c> are present, the file cannot be read,
    /// is not valid JSON, contains an unknown member, or contains an unknown dictionary key.
    /// </exception>
    public static BuildvanaConfig Load(string homeDirectory)
    {
        Guard.IsNotNullOrEmpty(homeDirectory);

        var jsonPath = Path.Combine(homeDirectory, JsonFileName);
        var jsoncPath = Path.Combine(homeDirectory, JsoncFileName);
        var hasJson = File.Exists(jsonPath);
        var hasJsonc = File.Exists(jsoncPath);

        BuildFailedException.ThrowIf(
            hasJson && hasJsonc,
            $"Both {JsonFileName} and {JsoncFileName} are present in {homeDirectory}. Keep only one.");

        var path = hasJson ? jsonPath
            : hasJsonc ? jsoncPath
            : null;
        if (path is null)
        {
            return new BuildvanaConfig();
        }

        var config = Parse(path);
        Validate(config, path);
        return config;
    }

    private static BuildvanaConfig Parse(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (IOException e)
        {
            throw new BuildFailedException($"Could not read from {path}: {e.Message}", e);
        }

        try
        {
            var config = JsonSerializer.Deserialize<BuildvanaConfig>(text, BuildvanaConfigSerialization.Options);
            return config ?? throw new BuildFailedException($"{path} was parsed as JSON null.");
        }
        catch (JsonException e)
        {
            throw new BuildFailedException($"{path} is not a valid Buildvana configuration file: {e.Message}", e);
        }
    }

    private static void Validate(BuildvanaConfig config, string path)
    {
        ValidateDictionaryKeys(config.DotNet?.Args?.Keys, AllowedDotNetArgsKeys, "dotnet.args", path);
        ValidateDictionaryKeys(config.NuGet?.Feeds?.Keys, AllowedNuGetFeedKeys, "nuget.feeds", path);
    }

    private static void ValidateDictionaryKeys(IEnumerable<string>? keys, string[] allowed, string section, string path)
    {
        if (keys is null)
        {
            return;
        }

        foreach (var key in keys)
        {
            BuildFailedException.ThrowIfNot(
                Array.IndexOf(allowed, key) >= 0,
                $"Unknown key '{key}' in {section} ({path}). Allowed keys: {string.Join(", ", allowed)}.");
        }
    }
}
