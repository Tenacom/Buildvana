// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

/*
 * Updates every dependency this repository pins, implementing "How to update dependencies" from
 * .claude/rules/dependency-management.md in one pass:
 *
 *   1. the .NET SDK version in global.json, from the official .NET release index;
 *   2. local dotnet tools, each through `dotnet tool update <id> --local --version <target>` (the blanket
 *      `--all` form would try to downgrade a prerelease pin — usually bv itself — to the latest stable and fail);
 *   3. PackageVersion pins in Directory.Packages.props;
 *   4. BV_PackageVersion pins in src/Buildvana.Sdk/Sdk/PackageVersions.props;
 *   5. the Roslyn floor properties in Directory.Packages.props (BV_MinRoslynVersion, BV_MinRoslynVersionHint,
 *      BV_SourceGeneratorsPackageFolder), derived from the Microsoft.CodeAnalysis.Common pin as it stands after
 *      step 3 — so a deliberate downgrade is expressed by editing the Microsoft.CodeAnalysis pins and re-running.
 *
 * Package targets follow .claude/rules/nuget-version-lookup.md: latest stable when the pin is stable; latest
 * prerelease of the same major.minor when the pin is a prerelease, falling back to latest stable when that line
 * has gone quiet; never a downgrade. Feeds come from nuget.config; non-HTTP(S) sources are skipped, and an
 * unreachable feed is skipped with a warning, per the rule.
 *
 * BV_MinRoslynVersionHint is derived from public metadata: the released SDK feature bands come from the .NET
 * release index, each band's compiler version from the Microsoft.Net.Compilers.Toolset dependency pinned in
 * eng/Version.Details.xml on the band's dotnet/sdk release branch, and the Visual Studio pairing from the band's
 * smallest vs-version in the channel's releases.json.
 *
 * Run from the repository root:
 *   dotnet run tools/update-dependencies.cs               # apply (default): rewrites the files in place
 *   dotnet run tools/update-dependencies.cs -- --check    # resolve and report only; exits 1 if anything is stale
 *
 * Either way, --all lists every pin in the report tables; by default only pins with a pending target (or that
 * could not be resolved) get a row.
 *
 * Exit codes: 0 = up to date, or applied cleanly; 1 = check found pending updates; 2 = usage or environment
 * error; 3 = a step could not complete (the warnings say which).
 */

#:package NuGet.Versioning
#:package Spectre.Console

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using NuGet.Versioning;
using Spectre.Console;

using FeedVersions = (
    bool Found,
    NuGet.Versioning.NuGetVersion? Stable,
    NuGet.Versioning.NuGetVersion? SameLinePrerelease,
    NuGet.Versioning.NuGetVersion? Preview);
using PinStatus = (
    string Id,
    NuGet.Versioning.NuGetVersion Current,
    NuGet.Versioning.NuGetVersion? Target,
    bool Found,
    NuGet.Versioning.NuGetVersion? Stable,
    NuGet.Versioning.NuGetVersion? Preview);

// Everything here runs on the only thread of a short-lived CLI: synchronous console writes and sub-second reads
// of small local files have nothing to yield to, and the async variants would only add noise.
#pragma warning disable CA1849 // Call async methods when in an async method

const string GlobalJsonPath = "global.json";
const string ToolManifestPath = ".config/dotnet-tools.json";
const string PackagesPropsPath = "Directory.Packages.props";
const string SdkPackagesPropsPath = "src/Buildvana.Sdk/Sdk/PackageVersions.props";
const string NuGetConfigPath = "nuget.config";
const string RoslynFloorPackageId = "Microsoft.CodeAnalysis.Common";
const string CompilersToolsetDependencyName = "Microsoft.Net.Compilers.Toolset";
const string ReleasesIndexUrl = "https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json";
const string SdkRepoRawUrlPrefix = "https://raw.githubusercontent.com/dotnet/sdk/release/";
const int TailLineCount = 15;

const string Usage = "Usage: dotnet run tools/update-dependencies.cs [-- [--check] [--all]]";

var check = false;
var all = false;
foreach (var argument in args)
{
    switch (argument)
    {
        case "--check" or "-c":
            check = true;
            break;
        case "--all":
            all = true;
            break;
        case "--help" or "-h" or "-?" or "/?":
            Console.WriteLine("Updates the .NET SDK version, local dotnet tools, NuGet package pins, and the Roslyn floor properties.");
            Console.WriteLine(Usage);
            Console.WriteLine("  -c, --check  Resolve and report only; exit 1 if anything is stale.");
            Console.WriteLine("      --all    List every pin in the report tables, not just the ones with news.");
            return 0;
        default:
            Console.Error.WriteLine($"Unknown argument: {argument}");
            Console.Error.WriteLine(Usage);
            return 2;
    }
}

string[] requiredFiles = [GlobalJsonPath, ToolManifestPath, PackagesPropsPath, SdkPackagesPropsPath, NuGetConfigPath];
var missingFiles = requiredFiles.Where(static path => !File.Exists(path)).ToList();
if (missingFiles.Count > 0)
{
    Console.Error.WriteLine($"Missing file(s): {string.Join(", ", missingFiles)}. Run from the repository root.");
    return 2;
}

// nuget.org serves registration data gzip-compressed; HttpClient does not decompress unless told to.
using var httpHandler = new HttpClientHandler
{
    AutomaticDecompression = DecompressionMethods.All,
    CheckCertificateRevocationList = true,
};
using var http = new HttpClient(httpHandler);
http.Timeout = TimeSpan.FromSeconds(30);
http.DefaultRequestHeaders.UserAgent.ParseAdd("Buildvana-update-dependencies/1.0");

var warnings = new ConcurrentQueue<string>();
using var throttle = new SemaphoreSlim(6);
var changeCount = 0;
var hardFailure = false;

// When output is redirected there is no real console width; pick one generous enough for the widest table.
if (Console.IsOutputRedirected)
{
    AnsiConsole.Console.Profile.Width = 200;
}

List<(string ChannelVersion, NuGetVersion LatestSdk, Uri ReleasesJsonUrl)>? channels = null;
try
{
    channels = await LoadStableChannelsAsync(http).ConfigureAwait(false);
}
catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
{
    warnings.Enqueue($"cannot load the .NET release index: {exception.Message}");
}

var (sources, skippedSources) = ReadConfiguredSources(NuGetConfigPath);
foreach (var skippedSource in skippedSources)
{
    Console.Error.WriteLine($"info: package source '{skippedSource}' is not an HTTP(S) feed; skipped.");
}

var feeds = new List<(string Name, string RegistrationBaseUrl)>();
foreach (var (sourceName, sourceUrl) in sources)
{
    try
    {
        var registrationBaseUrl = await GetRegistrationBaseUrlAsync(http, sourceUrl).ConfigureAwait(false);
        if (registrationBaseUrl is null)
        {
            warnings.Enqueue($"package source '{sourceName}' exposes no registration resource; skipped.");
            continue;
        }

        feeds.Add((sourceName, registrationBaseUrl));
    }
    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
    {
        warnings.Enqueue($"package source '{sourceName}' is unreachable ({exception.Message}); skipped.");
    }
}

if (feeds.Count == 0)
{
    Console.Error.WriteLine("No usable package source: every source configured in nuget.config was skipped or unreachable.");
    return 2;
}

// Step 1 — the .NET SDK version in global.json.
AnsiConsole.Write(new Rule($".NET SDK ({GlobalJsonPath})") { Justification = Justify.Left });
var globalJsonText = File.ReadAllText(GlobalJsonPath);
var currentSdkText = ReadGlobalJsonSdkVersion(globalJsonText);
NuGetVersion? effectiveSdk = null;
if (currentSdkText is null || !NuGetVersion.TryParse(currentSdkText, out var currentSdk))
{
    warnings.Enqueue($"cannot read sdk.version from {GlobalJsonPath}; step skipped.");
    hardFailure = true;
}
else if (channels is null || channels.Count == 0)
{
    AnsiConsole.MarkupLine("  [yellow]skipped: the .NET release index is unavailable[/]");
    hardFailure = true;
    effectiveSdk = currentSdk;
}
else
{
    var latestSdk = channels.Max(static entry => entry.LatestSdk)!;
    effectiveSdk = latestSdk > currentSdk ? latestSdk : currentSdk;
    if (latestSdk > currentSdk)
    {
        AnsiConsole.MarkupLine($"  sdk.version: {currentSdkText} [green]->[/] {latestSdk.ToNormalizedString()}");
        changeCount++;
        if (!check)
        {
            var oldFragment = $"\"version\": \"{currentSdkText}\"";
            var newFragment = $"\"version\": \"{latestSdk.ToNormalizedString()}\"";
            if (TryReplaceOnce(ref globalJsonText, oldFragment, newFragment))
            {
                File.WriteAllText(GlobalJsonPath, globalJsonText);
            }
            else
            {
                warnings.Enqueue($"cannot locate the sdk.version fragment in {GlobalJsonPath}; not updated.");
                hardFailure = true;
            }
        }
    }
    else
    {
        AnsiConsole.MarkupLine($"  sdk.version: {currentSdkText} [grey](up to date)[/]");
    }
}

// Step 2 — local dotnet tools. Both modes resolve the manifest's pins with the same policy as package pins;
// apply mode then delegates each update to `dotnet tool update`, which installs as well as edits the manifest.
AnsiConsole.WriteLine();
AnsiConsole.Write(new Rule($"Local dotnet tools ({ToolManifestPath})") { Justification = Justify.Left });
var toolResults = await ResolveTargetsAsync(ReadToolPins(ToolManifestPath)).ConfigureAwait(false);
var toolReport = ReportPinResults(toolResults);
changeCount += toolReport.Changed;
if (toolReport.Unresolved > 0)
{
    hardFailure = true;
}

if (!check)
{
    // `dotnet tool update --local --all` is no use here: for a pin on a prerelease line it insists on the latest
    // stable release — a downgrade, for a tool whose stable line lags its previews — and fails the whole run
    // refusing to do it. Each tool is updated explicitly instead, to the version the shared policy resolves,
    // still through `dotnet tool update` so that the manifest edit and the installation stay the CLI's business.
    foreach (var toolResult in toolResults)
    {
        if (toolResult.Target is null)
        {
            continue;
        }

        var targetText = toolResult.Target.ToNormalizedString();
        string[] updateArguments = ["tool", "update", toolResult.Id, "--local", "--version", targetText];
        var (updateExitCode, updateOutput) = RunProcess("dotnet", updateArguments);
        if (updateExitCode != 0)
        {
            warnings.Enqueue($"`dotnet tool update {toolResult.Id} --local --version {targetText}` failed.");
            hardFailure = true;
            foreach (var line in updateOutput.TakeLast(TailLineCount))
            {
                Console.Error.WriteLine(line);
            }
        }
    }
}

// Step 3 — PackageVersion pins.
AnsiConsole.WriteLine();
AnsiConsole.Write(new Rule($"Package pins ({PackagesPropsPath})") { Justification = Justify.Left });
var packageResults = await ResolveTargetsAsync(ReadPins(PackagesPropsPath, "PackageVersion")).ConfigureAwait(false);
var packageReport = ReportPinResults(packageResults);
changeCount += packageReport.Changed;
if (packageReport.Unresolved > 0 || (!check && !ApplyPinChanges(PackagesPropsPath, packageResults)))
{
    hardFailure = true;
}

// Step 4 — BV_PackageVersion pins (packages the SDK injects into projects).
AnsiConsole.WriteLine();
AnsiConsole.Write(new Rule($"SDK-injected packages ({SdkPackagesPropsPath})") { Justification = Justify.Left });
var sdkPackageResults = await ResolveTargetsAsync(ReadPins(SdkPackagesPropsPath, "BV_PackageVersion")).ConfigureAwait(false);
var sdkPackageReport = ReportPinResults(sdkPackageResults);
changeCount += sdkPackageReport.Changed;
if (sdkPackageReport.Unresolved > 0 || (!check && !ApplyPinChanges(SdkPackagesPropsPath, sdkPackageResults)))
{
    hardFailure = true;
}

// Step 5 — the Roslyn floor properties, derived from the Microsoft.CodeAnalysis.Common pin as of step 3.
// All-or-nothing: when the hint cannot be derived, the whole trio is left alone rather than made inconsistent.
AnsiConsole.WriteLine();
AnsiConsole.Write(new Rule($"Roslyn floor ({PackagesPropsPath})") { Justification = Justify.Left });
var floorSource = packageResults.FirstOrDefault(
    static result => string.Equals(result.Id, RoslynFloorPackageId, StringComparison.OrdinalIgnoreCase));
if (floorSource.Id is null)
{
    warnings.Enqueue($"no {RoslynFloorPackageId} pin in {PackagesPropsPath}; Roslyn floor step skipped.");
    hardFailure = true;
}
else if (channels is null || channels.Count == 0)
{
    AnsiConsole.MarkupLine("  [yellow]skipped: the .NET release index is unavailable[/]");
    hardFailure = true;
}
else
{
    var floor = floorSource.Target ?? floorSource.Current;
    var expectedMin = $"{floor.Major}.{floor.Minor}";
    var expectedFolder = $"roslyn{expectedMin}";
    var band = await FindMinimumBandAsync(http, channels, floor.Major, floor.Minor, warnings).ConfigureAwait(false);
    NuGetVersion? vsVersion = null;
    if (band is not null)
    {
        try
        {
            vsVersion = await GetBandVsVersionAsync(http, band.Value.ReleasesJsonUrl, band.Value.Band).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            warnings.Enqueue($"cannot read the channel release data for the Visual Studio pairing: {exception.Message}");
        }
    }

    if (band is null)
    {
        warnings.Enqueue($"no released .NET SDK feature band ships a compiler at least {expectedMin}; Roslyn floor step skipped.");
        hardFailure = true;
    }
    else if (vsVersion is null)
    {
        var bandText = $"{band.Value.ChannelVersion}.{band.Value.Band}xx";
        warnings.Enqueue($"no Visual Studio pairing found for SDK {bandText}; Roslyn floor step skipped.");
        hardFailure = true;
    }
    else
    {
        var (bandChannel, bandNumber, _) = band.Value;
        var vsProductName = vsVersion.Major switch
        {
            17 => "2022",
            18 => "2026",
            _ => null,
        };
        if (vsProductName is null)
        {
            warnings.Enqueue($"no product name known for Visual Studio major version {vsVersion.Major}; extend the map in this tool.");
        }

        var vsDisplay = vsProductName is null
            ? $"{vsVersion.Major}.{vsVersion.Minor}+"
            : $"{vsProductName} {vsVersion.Major}.{vsVersion.Minor}+";
        var expectedHint = $".NET SDK {bandChannel}.{bandNumber}xx / Visual Studio {vsDisplay}";

        (string Property, string Expected)[] floorProperties = [
            ("BV_MinRoslynVersion", expectedMin),
            ("BV_MinRoslynVersionHint", expectedHint),
            ("BV_SourceGeneratorsPackageFolder", expectedFolder),
        ];
        var propsText = File.ReadAllText(PackagesPropsPath);
        var propsDocument = XDocument.Parse(propsText);
        var trioChanged = false;
        foreach (var (floorProperty, expectedValue) in floorProperties)
        {
            var currentValue = propsDocument.Descendants(floorProperty).FirstOrDefault()?.Value;
            if (currentValue is null)
            {
                warnings.Enqueue($"property {floorProperty} not found in {PackagesPropsPath}; not updated.");
                hardFailure = true;
                continue;
            }

            if (string.Equals(currentValue, expectedValue, StringComparison.Ordinal))
            {
                AnsiConsole.MarkupLine($"  {floorProperty}: {Markup.Escape(currentValue)} [grey](up to date)[/]");
                continue;
            }

            AnsiConsole.MarkupLine($"  {floorProperty}: {Markup.Escape(currentValue)} [green]->[/] {Markup.Escape(expectedValue)}");
            changeCount++;
            trioChanged = true;
            if (!check)
            {
                var oldElementText = $"<{floorProperty}>{currentValue}</{floorProperty}>";
                var newElementText = $"<{floorProperty}>{expectedValue}</{floorProperty}>";
                if (!TryReplaceOnce(ref propsText, oldElementText, newElementText))
                {
                    warnings.Enqueue($"cannot locate {floorProperty} in {PackagesPropsPath}; not updated.");
                    hardFailure = true;
                }
            }
        }

        if (!check && trioChanged)
        {
            File.WriteAllText(PackagesPropsPath, propsText);
        }

        var requiredSdkFloor = NuGetVersion.Parse($"{bandChannel}.{bandNumber}00");
        if (effectiveSdk is not null && effectiveSdk < requiredSdkFloor)
        {
            var floorText = $"{bandChannel}.{bandNumber}xx";
            warnings.Enqueue($"global.json pins SDK {effectiveSdk}, older than {floorText} required by Roslyn {expectedMin}.");
        }
    }
}

if (!warnings.IsEmpty)
{
    Console.Error.WriteLine();
    foreach (var warning in warnings)
    {
        Console.Error.WriteLine($"warning: {warning}");
    }
}

AnsiConsole.WriteLine();
var summary = check && changeCount == 0 ? "[green]Everything is up to date.[/]"
    : check ? $"[yellow]Check mode: {changeCount} pending update(s); nothing was modified.[/]"
    : changeCount == 0 ? "[green]Everything was already up to date.[/]"
    : $"[green]Applied {changeCount} update(s).[/]";
AnsiConsole.MarkupLine(summary);

if (hardFailure)
{
    Console.Error.WriteLine("error: one or more steps could not complete; see the warnings above.");
    return 3;
}

return check && changeCount > 0 ? 1 : 0;

async Task<List<PinStatus>> ResolveTargetsAsync(IReadOnlyList<(string Id, NuGetVersion Version)> pins)
{
    var tasks = pins.Select(async pin =>
    {
        await throttle.WaitAsync().ConfigureAwait(false);
        try
        {
            var market = await ResolveTargetAsync(http, feeds, pin.Id, pin.Version, warnings).ConfigureAwait(false);
            return (pin.Id, pin.Version, market.Target, market.Found, market.Stable, market.Preview);
        }
        finally
        {
            _ = throttle.Release();
        }
    });

    return [.. await Task.WhenAll(tasks).ConfigureAwait(false)];
}

// The point of the table is deciding, not just confirming — "latest stable" and "latest preview" show what
// exists beyond what the policy takes automatically (e.g. a new prerelease line). By default only pins with a
// pending target or an unresolved lookup get a row; --all lists them all.
(int Changed, int Unresolved) ReportPinResults(List<PinStatus> results)
{
    var changedCount = 0;
    var unresolvedCount = 0;
    var hiddenCount = 0;
    var table = new Table().Border(TableBorder.Simple);
    _ = table.AddColumns("package", "pinned", "target", "latest stable", "latest preview");
    foreach (var result in results)
    {
        if (!result.Found)
        {
            unresolvedCount++;
        }

        if (result.Target is not null)
        {
            changedCount++;
        }

        var noteworthy = result.Target is not null || !result.Found;
        if (!all && !noteworthy)
        {
            hiddenCount++;
            continue;
        }

        var targetCell = result.Target is null
            ? new Text("-")
            : new Text(result.Target.ToNormalizedString(), new Style(Color.Green));
        var stableCell = VersionCell(result.Found, result.Stable);
        var previewCell = VersionCell(result.Found, result.Preview);
        var pinnedText = result.Current.OriginalVersion ?? result.Current.ToNormalizedString();
        _ = table.AddRow(new Text(result.Id), new Text(pinnedText), targetCell, stableCell, previewCell);
    }

    if (table.Rows.Count > 0)
    {
        AnsiConsole.Write(table);
    }

    if (hiddenCount == results.Count && hiddenCount > 0)
    {
        AnsiConsole.MarkupLine($"  [grey]all {results.Count} pin(s) up to date[/]");
    }
    else if (hiddenCount > 0)
    {
        AnsiConsole.MarkupLine($"  [grey]({hiddenCount} up-to-date pin(s) not listed; use --all to list them)[/]");
    }

    return (changedCount, unresolvedCount);
}

bool ApplyPinChanges(string path, List<PinStatus> results)
{
    var changed = results.Where(static result => result.Target is not null).ToList();
    if (changed.Count == 0)
    {
        return true;
    }

    // Pins are edited by splicing the raw file text, never by an XML round-trip, so that formatting and comments
    // survive byte for byte. This relies on attributes appearing in `Include="..." Version="..."` order, which is
    // how both props files are written.
    var succeeded = true;
    var text = File.ReadAllText(path);
    foreach (var pin in changed)
    {
        var pinId = pin.Id;
        var oldFragment = $"Include=\"{pinId}\" Version=\"{pin.Current.OriginalVersion}\"";
        var newFragment = $"Include=\"{pinId}\" Version=\"{pin.Target!.ToNormalizedString()}\"";
        if (!TryReplaceOnce(ref text, oldFragment, newFragment))
        {
            warnings.Enqueue($"cannot locate the pin for {pinId} in {path}; not updated.");
            succeeded = false;
        }
    }

    File.WriteAllText(path, text);
    return succeeded;
}

static async Task<List<(string ChannelVersion, NuGetVersion LatestSdk, Uri ReleasesJsonUrl)>> LoadStableChannelsAsync(
    HttpClient http)
{
    var text = await FetchTextOrNullAsync(http, new Uri(ReleasesIndexUrl)).ConfigureAwait(false)
        ?? throw new HttpRequestException($"{ReleasesIndexUrl} was not found.");
    using var document = JsonDocument.Parse(text);
    var channels = new List<(string ChannelVersion, NuGetVersion LatestSdk, Uri ReleasesJsonUrl)>();
    if (!document.RootElement.TryGetProperty("releases-index", out var entries) || entries.ValueKind != JsonValueKind.Array)
    {
        return channels;
    }

    foreach (var entry in entries.EnumerateArray())
    {
        var isStablePhase = GetString(entry, "support-phase") is "active" or "maintenance";
        var channelVersion = GetString(entry, "channel-version");
        if (!isStablePhase || channelVersion.Length == 0)
        {
            continue;
        }

        if (!NuGetVersion.TryParse(GetString(entry, "latest-sdk"), out var latestSdk) || latestSdk.IsPrerelease)
        {
            continue;
        }

        if (Uri.TryCreate(GetString(entry, "releases.json"), UriKind.Absolute, out var releasesJsonUrl))
        {
            channels.Add((channelVersion, latestSdk, releasesJsonUrl));
        }
    }

    return channels;
}

static (List<(string Name, Uri Url)> Sources, List<string> Skipped) ReadConfiguredSources(string path)
{
    var sources = new List<(string Name, Uri Url)>();
    var skipped = new List<string>();
    var packageSources = XDocument.Load(path).Root?.Element("packageSources");
    if (packageSources is null)
    {
        return (sources, skipped);
    }

    foreach (var element in packageSources.Elements())
    {
        if (string.Equals(element.Name.LocalName, "clear", StringComparison.Ordinal))
        {
            sources.Clear();
            continue;
        }

        if (!string.Equals(element.Name.LocalName, "add", StringComparison.Ordinal))
        {
            continue;
        }

        var key = (string?)element.Attribute("key") ?? "(unnamed)";
        var value = (string?)element.Attribute("value") ?? string.Empty;
        if (Uri.TryCreate(value, UriKind.Absolute, out var url) && IsWebUrl(url))
        {
            sources.Add((key, url));
        }
        else
        {
            skipped.Add(key);
        }
    }

    return (sources, skipped);
}

static bool IsWebUrl(Uri url) => string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
    || string.Equals(url.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal);

static async Task<string?> GetRegistrationBaseUrlAsync(HttpClient http, Uri serviceIndexUrl)
{
    var text = await FetchTextOrNullAsync(http, serviceIndexUrl).ConfigureAwait(false);
    if (text is null)
    {
        return null;
    }

    using var document = JsonDocument.Parse(text);
    if (!document.RootElement.TryGetProperty("resources", out var resources) || resources.ValueKind != JsonValueKind.Array)
    {
        return null;
    }

    string[] preferredTypes = ["RegistrationsBaseUrl/3.6.0", "RegistrationsBaseUrl/3.0.0-beta", "RegistrationsBaseUrl"];
    foreach (var preferredType in preferredTypes)
    {
        foreach (var resource in resources.EnumerateArray())
        {
            if (!ResourceHasType(resource, preferredType))
            {
                continue;
            }

            var id = GetString(resource, "@id");
            if (id.Length > 0)
            {
                return id.EndsWith('/') ? id : id + "/";
            }
        }
    }

    return null;
}

static bool ResourceHasType(JsonElement resource, string type)
{
    if (!resource.TryGetProperty("@type", out var typeElement))
    {
        return false;
    }

    if (typeElement.ValueKind == JsonValueKind.String)
    {
        return string.Equals(typeElement.GetString(), type, StringComparison.Ordinal);
    }

    if (typeElement.ValueKind != JsonValueKind.Array)
    {
        return false;
    }

    foreach (var element in typeElement.EnumerateArray())
    {
        if (element.ValueKind == JsonValueKind.String && string.Equals(element.GetString(), type, StringComparison.Ordinal))
        {
            return true;
        }
    }

    return false;
}

static string? ReadGlobalJsonSdkVersion(string globalJsonText)
{
    using var document = JsonDocument.Parse(globalJsonText);
    if (!document.RootElement.TryGetProperty("sdk", out var sdk))
    {
        return null;
    }

    var version = GetString(sdk, "version");
    return version.Length > 0 ? version : null;
}

static List<(string Id, NuGetVersion Version)> ReadToolPins(string path)
{
    var pins = new List<(string Id, NuGetVersion Version)>();
    using var document = JsonDocument.Parse(File.ReadAllText(path));
    if (!document.RootElement.TryGetProperty("tools", out var tools) || tools.ValueKind != JsonValueKind.Object)
    {
        return pins;
    }

    foreach (var property in tools.EnumerateObject())
    {
        if (NuGetVersion.TryParse(GetString(property.Value, "version"), out var version))
        {
            pins.Add((property.Name, version));
        }
    }

    return pins;
}

static (int ExitCode, List<string> Lines) RunProcess(string executable, IReadOnlyList<string> arguments)
{
    var startInfo = new ProcessStartInfo(executable)
    {
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
    void Collect(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is null)
        {
            return;
        }

        lock (lines)
        {
            lines.Add(e.Data);
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

static List<(string Id, NuGetVersion Version)> ReadPins(string path, string itemName)
{
    var pins = new List<(string Id, NuGetVersion Version)>();
    foreach (var element in XDocument.Load(path).Descendants(itemName))
    {
        var id = (string?)element.Attribute("Include");
        var versionText = (string?)element.Attribute("Version");
        if (id is null or { Length: 0 } || versionText is null)
        {
            continue;
        }

        if (NuGetVersion.TryParse(versionText, out var version))
        {
            pins.Add((id, version));
        }
    }

    return pins;
}

static async Task<(NuGetVersion? Target, bool Found, NuGetVersion? Stable, NuGetVersion? Preview)> ResolveTargetAsync(
    HttpClient http,
    IReadOnlyList<(string Name, string RegistrationBaseUrl)> feeds,
    string packageId,
    NuGetVersion current,
    ConcurrentQueue<string> warnings)
{
    NuGetVersion? bestStable = null;
    NuGetVersion? bestSameLine = null;
    NuGetVersion? bestPreview = null;
    var foundCount = 0;
    foreach (var (feedName, registrationBaseUrl) in feeds)
    {
        try
        {
            var result = await GetFeedBestVersionsAsync(http, registrationBaseUrl, packageId, current).ConfigureAwait(false);
            if (result.Found)
            {
                foundCount++;
            }

            bestStable = MaxVersion(bestStable, result.Stable);
            bestSameLine = MaxVersion(bestSameLine, result.SameLinePrerelease);
            bestPreview = MaxVersion(bestPreview, result.Preview);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            warnings.Enqueue($"package source '{feedName}' failed for {packageId} ({exception.Message}); skipped.");
        }
    }

    // Nothing was actually seen: whatever the reason (every feed failed, or none hosts the package), an "up to
    // date" claim would be a lie, so the pin is reported as unresolved instead.
    if (foundCount == 0)
    {
        warnings.Enqueue($"{packageId} was not found on any reachable package source.");
        return (null, false, null, null);
    }

    // For a prerelease pin, a same-line prerelease wins as long as the line still advances; once it has gone
    // quiet (its newest prerelease is not ahead of the pin), the latest stable takes over — that is how a line
    // that stabilized gets its pin moved onto the stable release instead of being stuck forever. Jumping to a
    // different prerelease line is never done automatically: that is a decision, taken by editing the pin, and
    // the report's "latest preview" column exists to surface the opportunity.
    NuGetVersion? candidate;
    if (!current.IsPrerelease)
    {
        candidate = bestStable;
    }
    else
    {
        var lineAdvanced = bestSameLine is not null && bestSameLine > current;
        candidate = lineAdvanced ? bestSameLine : bestStable;
    }

    var target = candidate is not null && candidate > current ? candidate : null;
    return (target, true, bestStable, bestPreview);
}

static async Task<FeedVersions> GetFeedBestVersionsAsync(
    HttpClient http,
    string registrationBaseUrl,
    string packageId,
    NuGetVersion current)
{
#pragma warning disable CA1308 // Normalize strings to uppercase — the NuGet v3 protocol requires lowercase package ids in URLs
    var indexUrl = new Uri($"{registrationBaseUrl}{packageId.ToLowerInvariant()}/index.json");
#pragma warning restore CA1308
    var text = await FetchTextOrNullAsync(http, indexUrl).ConfigureAwait(false);
    if (text is null)
    {
        return (false, null, null, null);
    }

    using var index = JsonDocument.Parse(text);
    if (!index.RootElement.TryGetProperty("items", out var pageArray) || pageArray.ValueKind != JsonValueKind.Array)
    {
        return (true, null, null, null);
    }

    var pages = new List<(NuGetVersion Upper, JsonElement Page)>();
    foreach (var page in pageArray.EnumerateArray())
    {
        if (NuGetVersion.TryParse(GetString(page, "upper"), out var upper))
        {
            pages.Add((upper, page));
        }
    }

    // Pages hold disjoint, ordered version ranges, so they are walked from the newest down and abandoned as soon
    // as nothing further down can matter. Walking in descending order means the first stable seen is the newest
    // stable, the first prerelease seen is the newest preview, and the same-line search is over once a page's
    // whole range sits below the pin's major.minor line.
    var trackSameLine = current.IsPrerelease;
    NuGetVersion? stable = null;
    NuGetVersion? sameLinePrerelease = null;
    NuGetVersion? preview = null;
    foreach (var (upper, page) in pages.OrderByDescending(static entry => entry.Upper))
    {
        var pageBelowLine = (upper.Major, upper.Minor).CompareTo((current.Major, current.Minor)) < 0;
        var sameLineDone = !trackSameLine || sameLinePrerelease is not null || pageBelowLine;
        if (stable is not null && preview is not null && sameLineDone)
        {
            break;
        }

        foreach (var version in await GetPageVersionsAsync(http, page).ConfigureAwait(false))
        {
            if (!version.IsPrerelease)
            {
                stable = MaxVersion(stable, version);
                continue;
            }

            preview = MaxVersion(preview, version);
            var sameLine = version.Major == current.Major && version.Minor == current.Minor;
            if (trackSameLine && sameLine)
            {
                sameLinePrerelease = MaxVersion(sameLinePrerelease, version);
            }
        }
    }

    return (true, stable, sameLinePrerelease, preview);
}

static async Task<List<NuGetVersion>> GetPageVersionsAsync(HttpClient http, JsonElement page)
{
    if (page.TryGetProperty("items", out var inlineItems) && inlineItems.ValueKind == JsonValueKind.Array)
    {
        return ExtractLeafVersions(inlineItems);
    }

    if (!Uri.TryCreate(GetString(page, "@id"), UriKind.Absolute, out var pageUrl))
    {
        return [];
    }

    var text = await FetchTextOrNullAsync(http, pageUrl).ConfigureAwait(false);
    if (text is null)
    {
        return [];
    }

    using var document = JsonDocument.Parse(text);
    if (!document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
    {
        return [];
    }

    return ExtractLeafVersions(items);
}

static List<NuGetVersion> ExtractLeafVersions(JsonElement items)
{
    var versions = new List<NuGetVersion>();
    foreach (var item in items.EnumerateArray())
    {
        if (!item.TryGetProperty("catalogEntry", out var catalogEntry) || catalogEntry.ValueKind != JsonValueKind.Object)
        {
            continue;
        }

        if (catalogEntry.TryGetProperty("listed", out var listed) && listed.ValueKind == JsonValueKind.False)
        {
            continue;
        }

        if (NuGetVersion.TryParse(GetString(catalogEntry, "version"), out var version))
        {
            versions.Add(version);
        }
    }

    return versions;
}

static async Task<(string ChannelVersion, int Band, Uri ReleasesJsonUrl)?> FindMinimumBandAsync(
    HttpClient http,
    IReadOnlyList<(string ChannelVersion, NuGetVersion LatestSdk, Uri ReleasesJsonUrl)> channels,
    int floorMajor,
    int floorMinor,
    ConcurrentQueue<string> warnings)
{
    // Compiler versions grow monotonically along the band sequence, so the walk goes from the newest band down
    // and stops at the first one whose compiler no longer meets the floor; the band seen just before it is the
    // lowest one that does. Bands are assumed contiguous from 1xx up to the channel's latest SDK.
    (string ChannelVersion, int Band, Uri ReleasesJsonUrl)? candidate = null;
    foreach (var channel in channels.OrderByDescending(static entry => entry.LatestSdk))
    {
        for (var band = channel.LatestSdk.Patch / 100; band >= 1; band--)
        {
            NuGetVersion? compilerVersion;
            try
            {
                compilerVersion = await GetBandCompilerVersionAsync(http, channel.ChannelVersion, band).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or XmlException)
            {
                warnings.Enqueue($"cannot read the compiler version for SDK {channel.ChannelVersion}.{band}xx: {exception.Message}");
                return candidate;
            }

            if (compilerVersion is null)
            {
                warnings.Enqueue($"no release branch or compiler pin found for SDK {channel.ChannelVersion}.{band}xx.");
                return candidate;
            }

            if ((compilerVersion.Major, compilerVersion.Minor).CompareTo((floorMajor, floorMinor)) < 0)
            {
                return candidate;
            }

            candidate = (channel.ChannelVersion, band, channel.ReleasesJsonUrl);
        }
    }

    return candidate;
}

static async Task<NuGetVersion?> GetBandCompilerVersionAsync(HttpClient http, string channelVersion, int band)
{
    var url = new Uri($"{SdkRepoRawUrlPrefix}{channelVersion}.{band}xx/eng/Version.Details.xml");
    var text = await FetchTextOrNullAsync(http, url).ConfigureAwait(false);
    if (text is null)
    {
        return null;
    }

    var dependency = XDocument.Parse(text).Descendants("Dependency").FirstOrDefault(
        static element => string.Equals((string?)element.Attribute("Name"), CompilersToolsetDependencyName, StringComparison.Ordinal));
    var versionText = (string?)dependency?.Attribute("Version");
    return versionText is not null && NuGetVersion.TryParse(versionText, out var version) ? version : null;
}

static async Task<NuGetVersion?> GetBandVsVersionAsync(HttpClient http, Uri releasesJsonUrl, int band)
{
    var text = await FetchTextOrNullAsync(http, releasesJsonUrl).ConfigureAwait(false);
    if (text is null)
    {
        return null;
    }

    using var document = JsonDocument.Parse(text);
    if (!document.RootElement.TryGetProperty("releases", out var releases) || releases.ValueKind != JsonValueKind.Array)
    {
        return null;
    }

    // Every servicing release re-states its SDKs with the Visual Studio version current at that time, so the
    // band's pairing is the smallest vs-version seen across all of the band's SDK entries.
    NuGetVersion? best = null;
    foreach (var release in releases.EnumerateArray())
    {
        foreach (var sdkEntry in EnumerateSdkEntries(release))
        {
            if (!NuGetVersion.TryParse(GetString(sdkEntry, "version"), out var sdkVersion) || sdkVersion.Patch / 100 != band)
            {
                continue;
            }

            if (NuGetVersion.TryParse(GetString(sdkEntry, "vs-version"), out var vsVersion))
            {
                best = MinVersion(best, vsVersion);
            }
        }
    }

    return best;
}

static IEnumerable<JsonElement> EnumerateSdkEntries(JsonElement release)
{
    if (release.TryGetProperty("sdk", out var sdk) && sdk.ValueKind == JsonValueKind.Object)
    {
        yield return sdk;
    }

    if (!release.TryGetProperty("sdks", out var sdks) || sdks.ValueKind != JsonValueKind.Array)
    {
        yield break;
    }

    foreach (var entry in sdks.EnumerateArray())
    {
        if (entry.ValueKind == JsonValueKind.Object)
        {
            yield return entry;
        }
    }
}

static bool TryReplaceOnce(ref string text, string oldValue, string newValue)
{
    var start = text.IndexOf(oldValue, StringComparison.Ordinal);
    if (start < 0)
    {
        return false;
    }

    if (text.IndexOf(oldValue, start + oldValue.Length, StringComparison.Ordinal) >= 0)
    {
        return false;
    }

    text = string.Concat(text.AsSpan(0, start), newValue, text.AsSpan(start + oldValue.Length));
    return true;
}

static async Task<string?> FetchTextOrNullAsync(HttpClient http, Uri url)
{
    using var response = await http.GetAsync(url).ConfigureAwait(false);
    if (response.StatusCode == HttpStatusCode.NotFound)
    {
        return null;
    }

    _ = response.EnsureSuccessStatusCode();
    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
}

static Text VersionCell(bool found, NuGetVersion? version)
{
    if (!found)
    {
        return new Text("(unknown)", new Style(Color.Yellow));
    }

    return version is null ? new Text("-") : new Text(version.ToNormalizedString());
}

static NuGetVersion? MaxVersion(NuGetVersion? left, NuGetVersion? right)
{
    if (left is null || right is null)
    {
        return left ?? right;
    }

    return right > left ? right : left;
}

static NuGetVersion? MinVersion(NuGetVersion? left, NuGetVersion? right)
{
    if (left is null || right is null)
    {
        return left ?? right;
    }

    return right < left ? right : left;
}

static string GetString(JsonElement element, string propertyName)
{
    if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
    {
        return string.Empty;
    }

    return property.ValueKind == JsonValueKind.String ? property.GetString() ?? string.Empty : string.Empty;
}
