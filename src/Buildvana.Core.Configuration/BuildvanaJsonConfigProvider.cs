// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.IO;
using Buildvana.Core.Json.Schema;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Provides the Buildvana configuration file of a home directory: which file it is, and the wire model of
/// what it says.
/// </summary>
/// <remarks>
/// <para>The file is validated against the configuration schema, and each problem is reported as a
/// diagnostic with its source location. Composition of the wire model into the resolved domain model happens
/// above this provider, through <see cref="BuildvanaConfigFactory"/>.</para>
/// <para><see cref="Path"/> and <see cref="Config"/> are each resolved on first read and cached — result and
/// exception alike — for the lifetime of the instance, as <c>HomeDirectoryProvider</c> does for the home
/// directory. Finding the file is this class's business alone, so the path a run reports and the file it parses
/// are the same by construction rather than by agreement between callers.</para>
/// </remarks>
public sealed class BuildvanaJsonConfigProvider
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly Lazy<string?> _lazyPath;
    private readonly Lazy<BuildvanaJsonConfig> _lazyConfig;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildvanaJsonConfigProvider"/> class.
    /// </summary>
    /// <param name="home">The provider of the home directory the configuration file is looked for in.</param>
    public BuildvanaJsonConfigProvider(IHomeDirectoryProvider home)
    {
        Guard.IsNotNull(home);
        _lazyPath = new Lazy<string?>(() => FindFile(home));

        // Reads the path through its own Lazy, so that a run holding both facts has probed exactly once.
        _lazyConfig = new Lazy<BuildvanaJsonConfig>(() => LoadFile(_lazyPath.Value));
    }

    /// <summary>
    /// Gets the absolute path of the configuration file, or <see langword="null"/> when the home directory
    /// holds none.
    /// </summary>
    /// <exception cref="BuildFailedException">
    /// Both configuration files (<c>buildvana.json</c> and <c>buildvana.jsonc</c>) are present.
    /// </exception>
    public string? Path => _lazyPath.Value;

    /// <summary>
    /// Gets the parsed wire model, or an empty <see cref="BuildvanaJsonConfig"/> when the home directory holds
    /// no configuration file.
    /// </summary>
    /// <exception cref="BuildFailedException">
    /// <para>Both configuration files (<c>buildvana.json</c> and <c>buildvana.jsonc</c>) are present,
    /// or the file cannot be read.</para>
    /// <para>The file is present but not valid JSON, or does not conform to the schema; in that case
    /// <see cref="BuildFailedException.Diagnostics"/> lists each problem with its source location.</para>
    /// </exception>
    public BuildvanaJsonConfig Config => _lazyConfig.Value;

    /// <summary>
    /// Loads the configuration file at an already-known path, bypassing both the search and the cache.
    /// </summary>
    /// <param name="path">The path of the configuration file, or <see langword="null"/> for none.</param>
    /// <returns>The parsed wire model, or an empty <see cref="BuildvanaJsonConfig"/> if <paramref name="path"/>
    /// is <see langword="null"/>.</returns>
    /// <exception cref="BuildFailedException">
    /// <para>The file cannot be read, is not valid JSON, or does not conform to the schema; in the latter cases
    /// <see cref="BuildFailedException.Diagnostics"/> lists each problem with its source location.</para>
    /// </exception>
    /// <remarks>
    /// <para>For the caller that must re-read a file it has just rewritten, and therefore wants the parse this
    /// instance has cached to be bypassed rather than reused. Everything else reads <see cref="Config"/>.</para>
    /// </remarks>
    public static BuildvanaJsonConfig LoadFile(string? path)
    {
        if (path is null)
        {
            return new BuildvanaJsonConfig();
        }

        var json = StripBom(UserFile.ReadAllBytes(path));

        // The source map reads the bytes rather than the parsed document, so building it first is what lets a
        // repeated member be refused before anything asks a JsonObject to hold one. It then serves the schema
        // validation below, which would otherwise walk the document a second time to locate its errors.
        var sourceMap = TryBuildSourceMap(json);
        if (sourceMap is not null)
        {
            ValidateNoDuplicateMembers(sourceMap, path);
        }

        var node = Parse(json, path);

        // Parse throws unless the document is well-formed JSON, and well-formed JSON always makes a map.
        Validate(node, sourceMap!, path);

        // Validation guarantees a non-null object at the root, so deserialization cannot return null here.
        return node!.Deserialize(BuildvanaJsonConfigContext.Default.BuildvanaJsonConfig) ?? new BuildvanaJsonConfig();
    }

    // The one probe for the configuration file: nothing outside this class asks which file a home directory
    // holds, so no two callers can answer differently.
    private static string? FindFile(IHomeDirectoryProvider home)
    {
        string[] candidatePaths =
        [
            System.IO.Path.Combine(home.HomeDirectory, BuildvanaJsonConfig.JsonFileName),
            System.IO.Path.Combine(home.HomeDirectory, BuildvanaJsonConfig.JsoncFileName),
        ];
        var existingPaths = Array.FindAll(candidatePaths, System.IO.File.Exists);
        if (existingPaths.Length > 1)
        {
            throw new BuildFailedException(
                $"Multiple Buildvana configuration files found: {string.Join(", ", existingPaths)}. Keep only one.");
        }

        return existingPaths.Length == 1 ? existingPaths[0] : null;
    }

    // Removes a leading UTF-8 byte order mark, if present, so the reader sees only JSON and positions start at 1.
    private static byte[] StripBom(byte[] bytes)
        => bytes is [0xEF, 0xBB, 0xBF, .. var rest] ? rest : bytes;

    // A document the map cannot read is not JSON at all, which Parse reports below with the reader's own
    // reason and position, so the failure is swallowed here rather than duplicated.
    private static JsonSourceMap? TryBuildSourceMap(byte[] json)
    {
        try
        {
            return JsonSourceMap.Build(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // JsonObject cannot hold a member name twice and throws an ArgumentException — naming the member, but not
    // where it is — as soon as it is asked to. Refusing a repeat here reports every one of them at once, under
    // a code of its own and with a position, as every other mistake in the file is reported.
    // System.Text.Json can refuse them too, through JsonDocumentOptions.AllowDuplicateProperties, but only as
    // a parse error carrying the first repeat: BV1108 would lose both its identity and the rest of the list.
    private static void ValidateNoDuplicateMembers(JsonSourceMap sourceMap, string path)
    {
        var duplicates = sourceMap.DuplicateMembers;
        if (duplicates.Count == 0)
        {
            return;
        }

        var diagnostics = new List<BuildDiagnostic>(duplicates.Count);
        foreach (var duplicate in duplicates)
        {
            diagnostics.Add(new BuildDiagnostic(
                BuildDiagnosticSeverity.Error,
                DiagnosticCodes.DuplicateProperty,
                $"Duplicate property '{duplicate.Name}'.",
                path,
                duplicate.Line,
                duplicate.Column));
        }

        throw new BuildFailedException($"Invalid configuration file {path}", diagnostics);
    }

    private static JsonNode? Parse(byte[] json, string path)
    {
        try
        {
            return JsonNode.Parse(json, documentOptions: DocumentOptions);
        }
        catch (JsonException e)
        {
            var (line, column) = ResolveParseErrorPosition(json, e);
            throw new BuildFailedException(
                $"Invalid JSON in {path}",
                [new BuildDiagnostic(BuildDiagnosticSeverity.Error, DiagnosticCodes.InvalidJson, e.Message, path, line, column)]);
        }
    }

    // Converts a JsonException's 0-based line and byte position into a 1-based line and *character* column, so a
    // parse-error location lines up with the character-based columns JsonSchemaValidator reports for schema errors.
    private static (int Line, int Column) ResolveParseErrorPosition(byte[] json, JsonException e)
    {
        var line = (int)(e.LineNumber ?? 0);
        var byteInLine = (int)(e.BytePositionInLine ?? 0);

        // Find the byte offset at which the error's line starts (just past the line-th newline).
        var lineStart = 0;
        var newlines = 0;
        for (var i = 0; newlines < line && i < json.Length; i++)
        {
            if (json[i] == (byte)'\n')
            {
                newlines++;
                lineStart = i + 1;
            }
        }

        // Count characters (not bytes) from the line start to the offending position, clamping in the unlikely
        // event the reported position lands past the end of the buffer.
        var available = json.Length - lineStart;
        var count = byteInLine < available ? byteInLine : available;
        var column = Encoding.UTF8.GetCharCount(json, lineStart, count) + 1;
        return (line + 1, column);
    }

    private static void Validate(JsonNode? node, JsonSourceMap sourceMap, string path)
    {
        var errors = JsonSchemaValidator.Validate<BuildvanaJsonConfig>(node, sourceMap, BuildvanaJsonConfigSerialization.Options);
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
            JsonSchemaErrorKind.TooShort => DiagnosticCodes.TooShort,
            JsonSchemaErrorKind.PatternMismatch => DiagnosticCodes.PatternMismatch,
            _ => throw new UnreachableException(),
        };
}
