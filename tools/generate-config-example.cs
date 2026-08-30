// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

/*
 * Generates or verifies buildvana.example.jsonc, the worked example of a Buildvana configuration file
 * (the walk lives in Buildvana.Core.Configuration, alongside the schema generator it reads).
 *
 * Run from the repository root:
 *   dotnet run tools/generate-config-example.cs                 # check mode (default): exits non-zero if the committed example is stale
 *   dotnet run tools/generate-config-example.cs -- --check      # same as above
 *   dotnet run tools/generate-config-example.cs -- --update     # regenerate and overwrite the committed example
 *
 * An optional trailing path argument overrides the example location (default: buildvana.example.jsonc relative to the working directory).
 *
 * Unlike the schema, the example needs no CI step: a test in Buildvana.Core.Configuration.Tests compares the
 * committed file against a fresh generation, so `dotnet test` catches staleness on CI and locally alike.
 * This tool exists for the update path.
 */

#:project ../src/Buildvana.Core.Configuration/Buildvana.Core.Configuration.csproj

using System;
using System.IO;
using Buildvana.Core.Configuration;

const string DefaultExamplePath = "buildvana.example.jsonc";

var update = false;
string? path = null;
foreach (var arg in args)
{
    switch (arg)
    {
        case "--update" or "-u":
            update = true;
            break;
        case "--check" or "-c":
            update = false;
            break;
        case "--help" or "-h" or "-?" or "/?":
            PrintUsage();
            return 0;
        default:
            if (arg.StartsWith('-'))
            {
                Console.Error.WriteLine($"Unknown option: {arg}");
                PrintUsage();
                return 2;
            }

            path = arg;
            break;
    }
}

var fullPath = Path.GetFullPath(path ?? DefaultExamplePath);

var generated = BuildvanaJsonConfigExample.Generate();

if (update)
{
    var directory = Path.GetDirectoryName(fullPath);
    if (!string.IsNullOrEmpty(directory))
    {
        Directory.CreateDirectory(directory);
    }

    File.WriteAllText(fullPath, generated);
    Console.WriteLine($"Regenerated example ({CountLines(generated)} lines) at:");
    Console.WriteLine($"  {fullPath}");
    return 0;
}

if (!File.Exists(fullPath))
{
    Console.Error.WriteLine($"Example file not found: {fullPath}");
    Console.Error.WriteLine("Run with --update to create it.");
    return 1;
}

// Compare on normalized line endings so a stray CRLF never masquerades as a real difference.
var committed = File.ReadAllText(fullPath).ReplaceLineEndings("\n");
if (string.Equals(committed, generated, StringComparison.Ordinal))
{
    Console.WriteLine($"Example is up to date ({CountLines(generated)} lines):");
    Console.WriteLine($"  {fullPath}");
    return 0;
}

Console.Error.WriteLine("Example is STALE: the committed file does not match the configuration model.");
Console.Error.WriteLine($"  {fullPath}");
Console.Error.WriteLine();
ReportFirstDifference(committed, generated);
Console.Error.WriteLine();
Console.Error.WriteLine("Run `dotnet run tools/generate-config-example.cs -- --update` to regenerate it, then commit the result.");
return 1;

static void PrintUsage()
{
    Console.WriteLine("Generates or verifies buildvana.example.jsonc from the Buildvana.Core.Configuration model.");
    Console.WriteLine();
    Console.WriteLine("Usage (from the repository root):");
    Console.WriteLine("  dotnet run tools/generate-config-example.cs [-- <options>] [path]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  -c, --check    Fail if the committed example is stale (default).");
    Console.WriteLine("  -u, --update   Regenerate and overwrite the committed example.");
    Console.WriteLine("  -h, --help     Show this help.");
    Console.WriteLine();
    Console.WriteLine($"The default example path is '{DefaultExamplePath}' relative to the working directory.");
}

// Generate() guarantees a trailing newline, so the line count is the number of line terminators.
static int CountLines(string text) => text.Split('\n').Length - 1;

// Reports the first line where the committed example diverges from the freshly generated one, with a little context.
static void ReportFirstDifference(string committed, string generated)
{
    var committedLines = committed.Split('\n');
    var generatedLines = generated.Split('\n');
    var max = Math.Max(committedLines.Length, generatedLines.Length);
    for (var i = 0; i < max; i++)
    {
        var committedLine = i < committedLines.Length ? committedLines[i] : "(end of file)";
        var generatedLine = i < generatedLines.Length ? generatedLines[i] : "(end of file)";
        if (!string.Equals(committedLine, generatedLine, StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"First difference at line {i + 1}:");
            Console.Error.WriteLine($"  committed:  {committedLine}");
            Console.Error.WriteLine($"  generated:  {generatedLine}");
            return;
        }
    }
}
