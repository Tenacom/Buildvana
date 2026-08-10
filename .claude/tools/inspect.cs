// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

/*
 * Runs the whole pre-push gate in one shot: `bv build` first, then ReSharper inspectcode, reporting the
 * diagnostics of both. Inspection only runs if the build reported nothing, because it analyzes the build output.
 *
 * Run from the repo root:
 *   `dotnet run .claude/tools/inspect.cs --gate`  pre-push gate: builds with `bv pack`, so that tests run and
 *                                                 artifacts are produced, inspects at WARNING severity and
 *                                                 above, and exits non-zero if anything at all was reported.
 *   `dotnet run .claude/tools/inspect.cs`         VS Code task: builds with `bv build`, which is all inspection
 *                                                 needs, inspects at SUGGESTION severity and above (hints are
 *                                                 excluded: the Problems panel cannot tell them from
 *                                                 suggestions anyway), and exits zero whatever it finds, so that
 *                                                 the task is not reported as failed for merely having found
 *                                                 something. A build or inspectcode *failure* still exits 2:
 *                                                 nothing was analyzed then, so a clean panel would be a lie.
 *   `dotnet run .claude/tools/inspect.cs --all`   as the above, but down to HINT severity, so that the Problems
 *                                                 panel lists hints too. Rejected together with --gate rather
 *                                                 than ignored, because a switch that silently does nothing is
 *                                                 worse than one that refuses.
 *
 * Both modes write one `path(line,col): severity ID: message` per diagnostic to standard output and nothing
 * else, which is what the `$msCompile` problem matcher consumes; progress and failure context go to standard
 * error. The raw output of both child processes, and the SARIF report, are left in `.buildvana-temp`.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

const string SolutionFileName = "Buildvana.slnx";
const int TailLineCount = 40;

const string Usage = "Usage: dotnet run .claude/tools/inspect.cs [--gate | --all]";

var gate = false;
var all = false;
foreach (var argument in args)
{
    if (string.Equals(argument, "--gate", StringComparison.Ordinal))
    {
        gate = true;
        continue;
    }

    if (string.Equals(argument, "--all", StringComparison.Ordinal))
    {
        all = true;
        continue;
    }

    Console.Error.WriteLine($"Unknown argument: {argument}");
    Console.Error.WriteLine(Usage);
    return 2;
}

if (gate && all)
{
    Console.Error.WriteLine("--all cannot be combined with --gate: the gate reports at WARNING severity and above.");
    Console.Error.WriteLine(Usage);
    return 2;
}

var repoRoot = FindRepoRoot(Environment.CurrentDirectory, SolutionFileName);
if (repoRoot.Length == 0)
{
    Console.Error.WriteLine($"Cannot find {SolutionFileName} in the current directory or any of its ancestors.");
    return 2;
}

var scratchPath = Path.Combine(repoRoot, ".buildvana-temp");
var sarifPath = Path.Combine(scratchPath, "inspect.sarif");

// bv's own narration: leveled lines (`info: ...`) and activity lines (`[1] Build: starting...`). Everything else
// a child writes is MSBuild's, which is far too verbose to echo.
var narrationRegex = new Regex(@"^(?:error|warning|info|detail|trace): |^\[\d+\] ", RegexOptions.CultureInvariant);

// MSBuild prints each diagnostic twice: once inline, prefixed with the build node id (`   29>path(4,10): ...`),
// and once indented in the end-of-build summary. Stripping the prefix makes the two occurrences identical, which
// is what makes them dedupable. The origin part is left alone: it is a file with a position for compiler and
// analyzer diagnostics, a bare project or tool name for the rest, and both forms are what `$msCompile` expects.
var nodePrefixRegex = new Regex(@"^\s*(?:\d+>)?\s*", RegexOptions.CultureInvariant);
var diagnosticRegex = new Regex(
    @"^.+?\s*:\s*(?<severity>error|warning)\s+[A-Za-z][A-Za-z0-9_]*\s*:",
    RegexOptions.CultureInvariant);

// The gate stands in for the whole pre-push sanity check, so it packs: that runs the tests and leaves the
// artifacts in place for inspection. Everything else only needs the build that `--no-build` inspection reads.
var buildCommand = gate ? "pack" : "build";
Console.Error.WriteLine($"info: running `bv {buildCommand}`...");
var build = Run("dotnet", ["bv", buildCommand, "--no-color", "--nologo"], repoRoot, narrationRegex.IsMatch);

// Only now: the build starts with a clean, which deletes the scratch directory.
_ = Directory.CreateDirectory(scratchPath);
File.WriteAllLines(Path.Combine(scratchPath, "build.log"), build.Lines);

var buildDiagnostics = new List<string>();
var seenDiagnostics = new HashSet<string>(StringComparer.Ordinal);
var buildErrorCount = 0;
var buildWarningCount = 0;
foreach (var line in build.Lines)
{
    var normalized = nodePrefixRegex.Replace(line, string.Empty).TrimEnd();
    var match = diagnosticRegex.Match(normalized);
    var isNewDiagnostic = match.Success && seenDiagnostics.Add(normalized);
    if (!isNewDiagnostic)
    {
        continue;
    }

    buildDiagnostics.Add(normalized);
    if (string.Equals(match.Groups["severity"].Value, "error", StringComparison.Ordinal))
    {
        buildErrorCount++;
    }
    else
    {
        buildWarningCount++;
    }
}

// A warning does not necessarily fail the build (`TreatWarningsAsErrors` does not cover every diagnostic), so
// neither the exit code nor the parsed diagnostics alone are a sufficient gate: it takes both.
var buildIsClean = buildDiagnostics.Count == 0 && build.ExitCode == 0;
if (!buildIsClean)
{
    if (gate)
    {
        Console.Error.WriteLine($"=== bv {buildCommand}: {buildErrorCount} error(s), {buildWarningCount} warning(s) ===");
    }

    foreach (var diagnostic in buildDiagnostics)
    {
        Console.WriteLine(diagnostic);
    }

    if (buildDiagnostics.Count == 0)
    {
        Console.Error.WriteLine($"bv {buildCommand} failed with exit code {build.ExitCode} and reported no parseable diagnostics.");
        Console.Error.WriteLine($"Last {TailLineCount} lines of {Path.Combine(scratchPath, "build.log")}:");
        foreach (var line in build.Lines.TakeLast(TailLineCount))
        {
            Console.Error.WriteLine(line);
        }
    }

    // A build that merely reported something is a finding, and the non-gate modes deliberately do not fail for
    // findings. A build that *failed* is not a finding: nothing was analyzed, so the result is not "clean", and
    // saying so takes an exit code — the diagnostics alone may carry no file position, in which case the Problems
    // panel stays empty and a zero exit reads as success. Same reasoning as the inspectcode failure below.
    return gate ? 1
        : build.ExitCode == 0 ? 0
        : 2;
}

// --all has to name the lowest severity rather than leave the option out: inspectcode's own default is
// WARNING, so omitting it would silently produce exactly what the gate produces.
var severity = all ? "HINT" : gate ? "WARNING" : "SUGGESTION";
Console.Error.WriteLine($"info: running inspectcode at severity {severity} (this takes a while)...");
string[] inspectArguments = [
    "dnx",
    "JetBrains.ReSharper.GlobalTools",
    "inspectcode",
    "--swea",
    $"--severity={severity}",
    $"--output={sarifPath}",
    "--format=Sarif",
    "--properties:Configuration=Release",
    "--no-build",
    SolutionFileName,
    "--yes",
];

var inspect = Run("dotnet", inspectArguments, repoRoot, _ => false);
File.WriteAllLines(Path.Combine(scratchPath, "inspect.log"), inspect.Lines);
if (inspect.ExitCode != 0)
{
    Console.Error.WriteLine($"inspectcode failed with exit code {inspect.ExitCode}.");
    Console.Error.WriteLine($"Last {TailLineCount} lines of {Path.Combine(scratchPath, "inspect.log")}:");
    foreach (var line in inspect.Lines.TakeLast(TailLineCount))
    {
        Console.Error.WriteLine(line);
    }

    return 2;
}

if (!File.Exists(sarifPath))
{
    Console.Error.WriteLine($"inspectcode produced no report at {sarifPath}.");
    return 2;
}

JsonDocument report;
try
{
    using var stream = File.OpenRead(sarifPath);
    report = JsonDocument.Parse(stream);
}
catch (JsonException exception)
{
    Console.Error.WriteLine($"Malformed SARIF at {sarifPath}: {exception.Message}");
    return 2;
}

var inspectDiagnostics = new List<string>();
var inspectErrorCount = 0;
var inspectWarningCount = 0;
var inspectInfoCount = 0;
using (report)
{
    var run = GetFirstRun(report.RootElement);

    // A result carries its own level only when it departs from its rule's configured one, so the rule table is
    // not an optimization: without it every inherited level would read as the fallback.
    var ruleLevels = GetRuleLevels(run);
    foreach (var result in GetResults(run))
    {
        var ruleId = GetString(result, "ruleId");
        var level = GetString(result, "level");
        if (level.Length == 0)
        {
            level = ruleLevels.TryGetValue(ruleId, out var ruleLevel) ? ruleLevel : "warning";
        }

        var word = level switch
        {
            "error" => "error",
            "warning" => "warning",
            _ => "info",
        };

        var message = result.TryGetProperty("message", out var messageElement) ? GetString(messageElement, "text") : string.Empty;
        var (path, line, column) = GetFirstLocation(result, repoRoot);
        inspectDiagnostics.Add($"{path}({line},{column}): {word} {ruleId}: {message}");
        switch (word)
        {
            case "error":
                inspectErrorCount++;
                break;
            case "warning":
                inspectWarningCount++;
                break;
            default:
                inspectInfoCount++;
                break;
        }
    }
}

if (gate)
{
    var counts = $"{inspectErrorCount} error(s), {inspectWarningCount} warning(s), {inspectInfoCount} info(s)";
    Console.Error.WriteLine($"=== inspectcode: {counts} ===");
}

foreach (var diagnostic in inspectDiagnostics)
{
    Console.WriteLine(diagnostic);
}

if (gate && inspectDiagnostics.Count == 0)
{
    Console.Error.WriteLine($"=== gate passed: neither bv {buildCommand} nor inspectcode reported anything ===");
}

return gate && inspectDiagnostics.Count > 0 ? 1 : 0;

static string FindRepoRoot(string startDirectory, string solutionFileName)
{
    for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, solutionFileName)))
        {
            return directory.FullName;
        }
    }

    return string.Empty;
}

static (int ExitCode, List<string> Lines) Run(
    string executable,
    IReadOnlyList<string> arguments,
    string workingDirectory,
    Func<string, bool> shouldEcho)
{
    var startInfo = new ProcessStartInfo(executable)
    {
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };

    foreach (var argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    var lines = new List<string>();
    using var process = new Process { StartInfo = startInfo };

    // Both streams are merged and lines are classified by shape, never by origin: which stream carries bv's own
    // narration depends on the bv version (up to 2.1.145-preview everything goes to standard output; the split
    // that sends narration to standard error is in the source but not yet in a published package), while an
    // MSBuild diagnostic is recognizable wherever it surfaces.
    void Collect(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is null)
        {
            return;
        }

        lock (lines)
        {
            lines.Add(e.Data);
            if (shouldEcho(e.Data))
            {
                Console.Error.WriteLine(e.Data);
            }
        }
    }

    process.OutputDataReceived += Collect;
    process.ErrorDataReceived += Collect;
    _ = process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    process.WaitForExit();
    return (process.ExitCode, lines);
}

static JsonElement GetFirstRun(JsonElement root)
{
    var hasRuns = root.TryGetProperty("runs", out var runs) && runs.ValueKind == JsonValueKind.Array;
    return hasRuns && runs.GetArrayLength() > 0 ? runs[0] : default;
}

static IEnumerable<JsonElement> GetResults(JsonElement run)
{
    if (run.ValueKind != JsonValueKind.Object)
    {
        return [];
    }

    if (!run.TryGetProperty("results", out var results))
    {
        return [];
    }

    return results.ValueKind == JsonValueKind.Array ? results.EnumerateArray() : [];
}

static Dictionary<string, string> GetRuleLevels(JsonElement run)
{
    var levels = new Dictionary<string, string>(StringComparer.Ordinal);
    if (run.ValueKind != JsonValueKind.Object)
    {
        return levels;
    }

    if (!run.TryGetProperty("tool", out var tool) || !tool.TryGetProperty("driver", out var driver))
    {
        return levels;
    }

    if (!driver.TryGetProperty("rules", out var rules) || rules.ValueKind != JsonValueKind.Array)
    {
        return levels;
    }

    foreach (var rule in rules.EnumerateArray())
    {
        if (!rule.TryGetProperty("defaultConfiguration", out var configuration))
        {
            continue;
        }

        var id = GetString(rule, "id");
        if (id.Length == 0)
        {
            continue;
        }

        var level = GetString(configuration, "level");
        if (level.Length > 0)
        {
            levels[id] = level;
        }
    }

    return levels;
}

static (string Path, int Line, int Column) GetFirstLocation(JsonElement result, string repoRoot)
{
    if (!result.TryGetProperty("locations", out var locations) || locations.ValueKind != JsonValueKind.Array)
    {
        return ("?", 1, 1);
    }

    if (locations.GetArrayLength() == 0 || !locations[0].TryGetProperty("physicalLocation", out var physical))
    {
        return ("?", 1, 1);
    }

    var uri = physical.TryGetProperty("artifactLocation", out var artifact) ? GetString(artifact, "uri") : string.Empty;
    var hasRegion = physical.TryGetProperty("region", out var region);
    var line = hasRegion ? GetInt32(region, "startLine", 1) : 1;
    var column = hasRegion ? GetInt32(region, "startColumn", 1) : 1;
    return (ResolvePath(repoRoot, uri), line, column);
}

static string ResolvePath(string repoRoot, string uri)
{
    if (uri.Length == 0)
    {
        return "?";
    }

    var unescaped = Uri.UnescapeDataString(uri);
    if (Uri.TryCreate(unescaped, UriKind.Absolute, out var absolute) && absolute.IsFile)
    {
        return absolute.LocalPath;
    }

    return Path.GetFullPath(Path.Combine(repoRoot, unescaped.Replace('/', Path.DirectorySeparatorChar)));
}

static string GetString(JsonElement element, string propertyName)
{
    if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
    {
        return string.Empty;
    }

    return property.ValueKind == JsonValueKind.String ? property.GetString() ?? string.Empty : string.Empty;
}

static int GetInt32(JsonElement element, string propertyName, int fallback)
{
    if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
    {
        return fallback;
    }

    return property.TryGetInt32(out var value) ? value : fallback;
}
