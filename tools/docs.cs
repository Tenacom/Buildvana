// Copyright (C) Tenacom and contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

/*
 * Builds the documentation site into .buildvana-temp/docs-site, or serves it on a local web server.
 *
 * Run from the home directory:
 *   dotnet run tools/docs.cs                         # build the site
 *   dotnet run tools/docs.cs -- serve                # build, serve on http://localhost:8080, and rebuild on every change
 *   dotnet run tools/docs.cs -- serve --port 9000    # the arguments after the command go to the generator
 *
 * The generator is the local tool Meziantou.DocumentationGenerator, which `dotnet tool restore` installs.
 * docgen.json configures it, and names the packages of the API reference with the folder where `bv pack` leaves
 * them. When that folder is missing, as after `bv clean`, the tool builds the pages alone, without the API
 * reference, and says so.
 *
 * The output folder is under .buildvana-temp, which git ignores and `bv clean` deletes.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

const string ConfigFileName = "docgen.json";
const string OutputPath = ".buildvana-temp/docs-site";

bool serve;
string[] generatorArgs;
switch (args)
{
    case ["--help" or "-h" or "-?" or "/?", ..]:
        PrintUsage();
        return 0;
    case ["build", .. var buildArgs]:
        serve = false;
        generatorArgs = buildArgs;
        break;
    case ["serve", .. var serveArgs]:
        serve = true;
        generatorArgs = serveArgs;
        break;
    case [] or [['-', ..], ..]:
        serve = false;
        generatorArgs = args;
        break;
    default:
        Console.Error.WriteLine($"Unknown command: {args[0]}");
        PrintUsage();
        return 2;
}

if (!File.Exists(ConfigFileName))
{
    Console.Error.WriteLine($"{ConfigFileName} is not in the current directory. Run this tool from the home directory.");
    return 2;
}

List<string> arguments = ["Meziantou.DocumentationGenerator", "build", "--output", OutputPath];
if (serve)
{
    arguments.AddRange(["--serve", "--watch"]);
}

if (FindMissingPackagePath(ConfigFileName) is { } missingPath)
{
    Console.Error.WriteLine($"info: {missingPath} holds no package, so the site gets no API reference. Run `dotnet bv pack` to get one.");
    arguments.AddRange(["--packages", "[]", "--references", "[]", "--allow-invalid-cref"]);
}

arguments.AddRange(generatorArgs);
var startInfo = new ProcessStartInfo("dotnet") { UseShellExecute = false };
foreach (var argument in arguments)
{
    startInfo.ArgumentList.Add(argument);
}

// Ctrl+C reaches the generator too, which stops its web server and exits. This tool waits for it, and returns its exit code.
Console.CancelKeyPress += static (_, e) => e.Cancel = true;
using var process = Process.Start(startInfo);
if (process is null)
{
    Console.Error.WriteLine("Could not start dotnet.");
    return 2;
}

process.WaitForExit();
return process.ExitCode;

static void PrintUsage()
{
    Console.WriteLine($"Builds the documentation site into {OutputPath}, or serves it on a local web server.");
    Console.WriteLine();
    Console.WriteLine("Usage (from the home directory):");
    Console.WriteLine("  dotnet run tools/docs.cs [-- [build|serve] [<generator options>]]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  build   Build the site (default).");
    Console.WriteLine("  serve   Build the site, serve it on http://localhost:8080, and rebuild it on every change.");
    Console.WriteLine();
    Console.WriteLine("The generator gets every other argument, such as `--port 9000` or `--verbosity detailed`.");
}

// The first path that an entry of `packages` in the configuration names and that holds no package, or null when
// every such path holds one. A path is a package file, or a folder of package files, relative to the configuration.
static string? FindMissingPackagePath(string configPath)
{
    var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    using var document = JsonDocument.Parse(File.ReadAllText(configPath), options);
    var hasPackages = document.RootElement.TryGetProperty("packages", out var packages) && packages.ValueKind == JsonValueKind.Array;
    if (!hasPackages)
    {
        return null;
    }

    return packages.EnumerateArray()
        .Select(static package => package.TryGetProperty("path", out var path) ? path.GetString() : null)
        .OfType<string>()
        .FirstOrDefault(static path => !File.Exists(path) && !HasPackageFiles(path));
}

static bool HasPackageFiles(string directory)
    => Directory.Exists(directory) && Directory.EnumerateFiles(directory, "*.nupkg").Any();
