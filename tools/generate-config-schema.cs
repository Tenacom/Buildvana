// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

/*
 * Generates or verifies schemas/buildvana.schema.json from the typed Buildvana.Core.Configuration model.
 *
 * Run from the repository root:
 *   dotnet run tools/generate-config-schema.cs                 # check mode (default): exits non-zero if the committed schema is stale
 *   dotnet run tools/generate-config-schema.cs -- --check      # same as above
 *   dotnet run tools/generate-config-schema.cs -- --update     # regenerate and overwrite the committed schema
 *
 * An optional trailing path argument overrides the schema location (default: schemas/buildvana.schema.json relative to the working directory).
 *
 * CI runs this in check mode so that a model change without a matching schema regeneration fails the build.
 * Humans run it in update mode after editing the model.
 */

#:project ../src/Buildvana.Core.Configuration/Buildvana.Core.Configuration.csproj

using System;
using System.IO;
using Buildvana.Core.Configuration;

const string DefaultSchemaPath = "schemas/buildvana.schema.json";

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

var fullPath = Path.GetFullPath(path ?? DefaultSchemaPath);

var generated = BuildvanaConfigSchema.Generate();

if (update)
{
    var directory = Path.GetDirectoryName(fullPath);
    if (!string.IsNullOrEmpty(directory))
    {
        Directory.CreateDirectory(directory);
    }

    File.WriteAllText(fullPath, generated);
    Console.WriteLine($"Regenerated schema ({CountLines(generated)} lines) at:");
    Console.WriteLine($"  {fullPath}");
    return 0;
}

if (!File.Exists(fullPath))
{
    Console.Error.WriteLine($"Schema file not found: {fullPath}");
    Console.Error.WriteLine("Run with --update to create it.");
    return 1;
}

// Compare on normalized line endings so a stray CRLF never masquerades as a real difference.
var committed = File.ReadAllText(fullPath).ReplaceLineEndings("\n");
if (string.Equals(committed, generated, StringComparison.Ordinal))
{
    Console.WriteLine($"Schema is up to date ({CountLines(generated)} lines):");
    Console.WriteLine($"  {fullPath}");
    return 0;
}

Console.Error.WriteLine("Schema is STALE: the committed file does not match the configuration model.");
Console.Error.WriteLine($"  {fullPath}");
Console.Error.WriteLine();
ReportFirstDifference(committed, generated);
Console.Error.WriteLine();
Console.Error.WriteLine("Run `dotnet run tools/generate-config-schema.cs -- --update` to regenerate it, then commit the result.");
return 1;

static void PrintUsage()
{
    Console.WriteLine("Generates or verifies buildvana.schema.json from the Buildvana.Core.Configuration model.");
    Console.WriteLine();
    Console.WriteLine("Usage (from the repository root):");
    Console.WriteLine("  dotnet run tools/generate-config-schema.cs [-- <options>] [path]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  -c, --check    Fail if the committed schema is stale (default).");
    Console.WriteLine("  -u, --update   Regenerate and overwrite the committed schema.");
    Console.WriteLine("  -h, --help     Show this help.");
    Console.WriteLine();
    Console.WriteLine($"The default schema path is '{DefaultSchemaPath}' relative to the working directory.");
}

static int CountLines(string text) => text.Split('\n').Length;

// Reports the first line where the committed schema diverges from the freshly generated one, with a little context.
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
