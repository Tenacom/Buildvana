// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

/*
 * Summarizes a ReSharper inspectcode SARIF report as one line per result.
 * Run from the repo root: `dotnet run .claude/tools/inspect-sarif.cs [path]`.
 * Default path is `inspect.sarif` in the current directory.
 */

using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

var path = args.Length > 0 ? args[0] : "inspect.sarif";
if (!File.Exists(path))
{
    Console.Error.WriteLine($"File not found: {path}");
    return 2;
}

JsonDocument doc;
try
{
    using var fs = File.OpenRead(path);
    doc = JsonDocument.Parse(fs);
}
catch (JsonException ex)
{
    Console.Error.WriteLine($"Malformed SARIF: {ex.Message}");
    return 2;
}

using (doc)
{
    var results = FindResults(doc.RootElement);
    if (results is null)
    {
        Console.WriteLine("Total results: 0");
        return 0;
    }

    var resultsArray = results.Value;
    Console.WriteLine($"Total results: {resultsArray.GetArrayLength()}");

    foreach (var r in resultsArray.EnumerateArray())
    {
        var ruleId = r.TryGetProperty("ruleId", out var rid) ? rid.GetString() ?? "?" : "?";
        var level = r.TryGetProperty("level", out var lvl) ? lvl.GetString() ?? "warning" : "warning";
        var message = r.TryGetProperty("message", out var msgEl) && msgEl.TryGetProperty("text", out var txtEl)
            ? txtEl.GetRawText()
            : "\"\"";

        var (file, line) = GetFirstLocation(r);
        Console.WriteLine($"[{level}] {ruleId} {file}:{line} — {message}");
    }
}

return 0;

static JsonElement? FindResults(JsonElement root)
{
    if (!root.TryGetProperty("runs", out var runs))
    {
        return null;
    }

    var hasFirstRun = runs.ValueKind == JsonValueKind.Array && runs.GetArrayLength() > 0;
    if (!hasFirstRun)
    {
        return null;
    }

    if (!runs[0].TryGetProperty("results", out var results))
    {
        return null;
    }

    return results.ValueKind == JsonValueKind.Array ? results : null;
}

static (string File, string Line) GetFirstLocation(JsonElement r)
{
    const string Unknown = "?";

    if (!r.TryGetProperty("locations", out var locs))
    {
        return (Unknown, Unknown);
    }

    var hasFirstLocation = locs.ValueKind == JsonValueKind.Array && locs.GetArrayLength() > 0;
    if (!hasFirstLocation)
    {
        return (Unknown, Unknown);
    }

    if (!locs[0].TryGetProperty("physicalLocation", out var phys))
    {
        return (Unknown, Unknown);
    }

    var file = phys.TryGetProperty("artifactLocation", out var artifact) && artifact.TryGetProperty("uri", out var uriEl)
        ? uriEl.GetString() ?? Unknown
        : Unknown;

    var line = phys.TryGetProperty("region", out var region) && region.TryGetProperty("startLine", out var lineEl)
        ? lineEl.GetInt32().ToString(CultureInfo.InvariantCulture)
        : Unknown;

    return (file, line);
}
