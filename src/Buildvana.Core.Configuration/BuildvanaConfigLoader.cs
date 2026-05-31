// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Buildvana.Core.JsonSchema;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Loads and validates the Buildvana configuration file found in a home directory.
/// </summary>
public static class BuildvanaConfigLoader
{
    private const string JsonFileName = "buildvana.json";
    private const string JsoncFileName = "buildvana.jsonc";

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Loads the configuration file found in <paramref name="homeDirectory"/>.
    /// </summary>
    /// <param name="homeDirectory">The home directory to search for a configuration file.</param>
    /// <returns>The parsed configuration, or an empty <see cref="BuildvanaConfig"/> if no file is present.</returns>
    /// <exception cref="BuildFailedException">
    /// <para>Both <c>buildvana.json</c> and <c>buildvana.jsonc</c> are present, or the file cannot be read.</para>
    /// <para>The file is present but not valid JSON, or does not conform to the schema; in that case
    /// <see cref="BuildFailedException.Diagnostics"/> lists each problem with its source location.</para>
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

        var json = StripBom(ReadAllBytes(path));
        var node = Parse(json, path);
        Validate(node, json, path);

        // Validation guarantees a non-null object at the root, so deserialization cannot return null here.
        return node!.Deserialize<BuildvanaConfig>(BuildvanaConfigSerialization.Options) ?? new BuildvanaConfig();
    }

    private static byte[] ReadAllBytes(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (IOException e)
        {
            throw new BuildFailedException($"Could not read from {path}: {e.Message}", e);
        }
    }

    // Removes a leading UTF-8 byte order mark, if present, so the reader sees only JSON and positions start at 1.
    private static byte[] StripBom(byte[] bytes)
        => bytes is [0xEF, 0xBB, 0xBF, .. var rest] ? rest : bytes;

    private static JsonNode? Parse(byte[] json, string path)
    {
        try
        {
            return JsonNode.Parse(json, documentOptions: DocumentOptions);
        }
        catch (JsonException e)
        {
            var line = (int)((e.LineNumber ?? 0) + 1);
            var column = (int)((e.BytePositionInLine ?? 0) + 1);
            throw new BuildFailedException(
                $"Invalid JSON in {path}",
                [new BuildDiagnostic(BuildDiagnosticSeverity.Error, DiagnosticCodes.InvalidJson, e.Message, path, line, column)]);
        }
    }

    private static void Validate(JsonNode? node, byte[] json, string path)
    {
        var errors = JsonSchemaValidator.Validate<BuildvanaConfig>(node, json, BuildvanaConfigSerialization.Options);
        if (errors.Count == 0)
        {
            return;
        }

        var diagnostics = new List<BuildDiagnostic>(errors.Count);
        foreach (var error in errors)
        {
            diagnostics.Add(new BuildDiagnostic(
                BuildDiagnosticSeverity.Error,
                CodeFor(error.Kind),
                error.Message,
                path,
                error.Line,
                error.Column));
        }

        throw new BuildFailedException($"Invalid configuration file {path}", diagnostics);
    }

    private static string CodeFor(JsonSchemaErrorKind kind)
        => kind switch
        {
            JsonSchemaErrorKind.TypeMismatch => DiagnosticCodes.TypeMismatch,
            JsonSchemaErrorKind.DisallowedValue => DiagnosticCodes.DisallowedValue,
            JsonSchemaErrorKind.UnknownProperty => DiagnosticCodes.UnknownProperty,
            JsonSchemaErrorKind.MissingProperty => DiagnosticCodes.MissingProperty,
            JsonSchemaErrorKind.ValueNotAllowed => DiagnosticCodes.ValueNotAllowed,
            _ => throw new UnreachableException(),
        };
}
