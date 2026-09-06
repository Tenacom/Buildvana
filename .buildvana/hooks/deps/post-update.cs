// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

/*
 * deps/post-update hook: keeps the toolchain floors in step with the Microsoft.CodeAnalysis.Common pin. Five
 * values in three files: BV_MinRoslynVersion, BV_MinRoslynVersionHint and BV_SourceGeneratorsPackageFolder in
 * Directory.Packages.props; BV_MinMSBuildVersion in src/Buildvana.Sdk/Sdk/Sdk.props; and the TOOLCHAIN-FLOORS
 * region of docs/introduction.md, a table stating the minimum .NET SDK, Visual Studio and MSBuild versions.
 *
 * BV_MinRoslynVersion is the pin's major.minor, and BV_SourceGeneratorsPackageFolder follows from it.
 * BV_MinRoslynVersionHint names the lowest released .NET SDK feature band whose compiler is at least that new,
 * paired with the Visual Studio version that shipped it. The bands come from the .NET release index, each band's
 * compiler version from the Microsoft.Net.Compilers.Toolset dependency pinned in eng/Version.Details.xml on the
 * band's dotnet/sdk release branch, and the pairing from the band's smallest vs-version in the channel's
 * releases.json. BV_MinMSBuildVersion is the paired Visual Studio version's major.minor, because MSBuild follows
 * the Visual Studio version, and the .NET SDK band ships the same MSBuild. The region states the same values.
 *
 * All or nothing: when one of the five values cannot be derived, or one of the files cannot be spliced, every
 * file is left alone rather than made inconsistent. A deliberate move of the floor is expressed by editing the
 * Microsoft.CodeAnalysis pins. The one exception is a Visual Studio major version absent from the product name
 * map below. The hint and the region then state a bare version number instead of a product name, and the hook
 * says so on stderr.
 *
 * A run that does not select the packages scope carries no Microsoft.CodeAnalysis.Common result. The hook then has
 * nothing to derive the floors from, and leaves the files alone.
 *
 * Exit codes: 0 = every value is right, or was corrected; 1 = a check run found one stale, which bv folds into
 * its own verdict; 2 = the derivation could not complete, which bv reports as its own exit code 3.
 */

#:package Buildvana.Runtime
#:package NuGet.Versioning

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Buildvana.Runtime;
using NuGet.Versioning;

// This hook runs on the only thread of a short-lived process, and reads three small local files: the async
// variants of the file calls would only add noise.
#pragma warning disable CA1849 // Call async methods when in an async method

const string RoslynFloorPackageId = "Microsoft.CodeAnalysis.Common";
const string CompilersToolsetDependencyName = "Microsoft.Net.Compilers.Toolset";
const string ReleasesIndexUrl = "https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json";
const string SdkRepoRawUrlPrefix = "https://raw.githubusercontent.com/dotnet/sdk/release/";
const string PackagesPropsPath = "Directory.Packages.props";
const string SdkPropsPath = "src/Buildvana.Sdk/Sdk/Sdk.props";
const string IntroductionPath = "docs/introduction.md";
const string MSBuildFloorPropertyName = "BV_MinMSBuildVersion";
const string FloorsRegionName = "TOOLCHAIN-FLOORS";
const int PendingWorkExitCode = 1;
const int DerivationFailedExitCode = 2;

var hookArgs = PostUpdateHookArgs.Load();

// The pin as the run leaves it: the version applied in an apply run, the foreseen one in a check run, and the
// current one when the run did not resolve the pin at all. The trio is derived from all three the same way.
var floorPin = hookArgs.Packages.FirstOrDefault(
    static result => string.Equals(result.Id, RoslynFloorPackageId, StringComparison.OrdinalIgnoreCase));
if (floorPin is null)
{
    // A run that does not select the packages scope carries no package results, and the floor has nothing to
    // derive from. Say so and leave the trio alone: failing here would fail every scope-limited run.
    Console.WriteLine($"No {RoslynFloorPackageId} result in this run: the Roslyn floor properties were left alone.");
    return 0;
}

var floorVersionText = floorPin.Target ?? floorPin.CurrentVersion;
if (!NuGetVersion.TryParse(floorVersionText, out var floorVersion))
{
    Console.Error.WriteLine($"{RoslynFloorPackageId} is pinned to {floorVersionText}, which is not a version.");
    return DerivationFailedExitCode;
}

var expectedVersion = $"{floorVersion.Major}.{floorVersion.Minor}";
var expectedFolder = $"roslyn{expectedVersion}";

// The release metadata is served compressed; HttpClient does not decompress unless told to.
using var httpHandler = new HttpClientHandler
{
    AutomaticDecompression = DecompressionMethods.All,
    CheckCertificateRevocationList = true,
};

using var http = new HttpClient(httpHandler);
http.Timeout = TimeSpan.FromSeconds(30);
http.DefaultRequestHeaders.UserAgent.ParseAdd("Buildvana-deps-post-update/1.0");

List<(string ChannelVersion, NuGetVersion LatestSdk, Uri ReleasesJsonUrl)> channels;
try
{
    channels = await LoadStableChannelsAsync(http).ConfigureAwait(false);
}
catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
{
    Console.Error.WriteLine($"Cannot load the .NET release index: {exception.Message}");
    return DerivationFailedExitCode;
}

var band = await FindMinimumBandAsync(http, channels, floorVersion.Major, floorVersion.Minor).ConfigureAwait(false);
if (band is null)
{
    // FindMinimumBandAsync has already said on stderr why it has no band to give.
    return DerivationFailedExitCode;
}

var (bandChannel, bandNumber, releasesJsonUrl) = band.Value;
NuGetVersion? vsVersion;
try
{
    vsVersion = await GetBandVsVersionAsync(http, releasesJsonUrl, bandNumber).ConfigureAwait(false);
}
catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
{
    Console.Error.WriteLine($"Cannot read the channel release data for the Visual Studio pairing: {exception.Message}");
    return DerivationFailedExitCode;
}

if (vsVersion is null)
{
    Console.Error.WriteLine($"No Visual Studio pairing found for .NET SDK {bandChannel}.{bandNumber}xx.");
    return DerivationFailedExitCode;
}

// The map covers the Visual Studio majors that .NET SDK bands have paired with so far. Extend it when a new
// major appears: without a product name the hint states a bare version number, which no download page uses.
var vsProductName = vsVersion.Major switch
{
    17 => "2022",
    18 => "2026",
    _ => null,
};
if (vsProductName is null)
{
    Console.Error.WriteLine($"No product name known for Visual Studio major version {vsVersion.Major}; extend the map in this hook.");
}

var vsDisplay = vsProductName is null
    ? $"{vsVersion.Major}.{vsVersion.Minor}+"
    : $"{vsProductName} {vsVersion.Major}.{vsVersion.Minor}+";
var expectedHint = $".NET SDK {bandChannel}.{bandNumber}xx / Visual Studio {vsDisplay}";

(string Name, string Expected)[] floorProperties = [
    ("BV_MinRoslynVersion", expectedVersion),
    ("BV_MinRoslynVersionHint", expectedHint),
    ("BV_SourceGeneratorsPackageFolder", expectedFolder),
];

// MSBuild follows the Visual Studio version, and the .NET SDK band ships the same MSBuild, so the MSBuild floor
// is the paired Visual Studio version. The region states the three minimums the way the hint does.
var vsVersionText = $"{vsVersion.Major}.{vsVersion.Minor}";
var expectedMSBuildVersion = vsVersionText;
var vsCell = vsProductName is null ? vsVersionText : $"{vsProductName} ({vsVersionText})";
var expectedRegionLines = FormatTable(
[
    ["Tool", "Minimum version"],
    [".NET SDK", $"{bandChannel}.{bandNumber}00"],
    ["Visual Studio", vsCell],
    ["MSBuild", expectedMSBuildVersion],
]);

// A check run splices too, and throws the result away: a property whose element cannot be located, or a region
// whose markers are missing or doubled, is then reported by the run that checks, instead of by the run that
// writes. Nothing is written until every splice has succeeded.
var homeDirectory = hookArgs.RuntimeInfo.HomeDirectory;
var packagesPropsText = File.ReadAllText(Path.Combine(homeDirectory, PackagesPropsPath));
var sdkPropsText = File.ReadAllText(Path.Combine(homeDirectory, SdkPropsPath));
var introductionText = File.ReadAllText(Path.Combine(homeDirectory, IntroductionPath));
var staleCount = 0;
foreach (var (propertyName, expectedValue) in floorProperties)
{
    if (!TrySpliceProperty(ref packagesPropsText, PackagesPropsPath, propertyName, expectedValue, ref staleCount))
    {
        return DerivationFailedExitCode;
    }
}

if (!TrySpliceProperty(ref sdkPropsText, SdkPropsPath, MSBuildFloorPropertyName, expectedMSBuildVersion, ref staleCount))
{
    return DerivationFailedExitCode;
}

if (!TrySpliceRegion(ref introductionText, IntroductionPath, FloorsRegionName, expectedRegionLines, ref staleCount))
{
    return DerivationFailedExitCode;
}

// The .NET SDK the repository pins must be able to run the compiler the floor demands. global.json still states
// the old version while the hook runs, so the comparison reads what the run made of the pin.
var requiredSdkFloor = NuGetVersion.Parse($"{bandChannel}.{bandNumber}00");
var sdkVersionText = hookArgs.NetSdk?.Target ?? hookArgs.NetSdk?.CurrentVersion ?? string.Empty;
var isSdkTooOld = NuGetVersion.TryParse(sdkVersionText, out var sdkVersion) && sdkVersion < requiredSdkFloor;
if (isSdkTooOld)
{
    Console.Error.WriteLine(
        $"global.json pins .NET SDK {sdkVersion}, older than the {bandChannel}.{bandNumber}xx that Roslyn {expectedVersion} needs.");
}

if (staleCount == 0)
{
    return 0;
}

if (hookArgs.Check)
{
    return PendingWorkExitCode;
}

WriteIfChanged(homeDirectory, PackagesPropsPath, packagesPropsText);
WriteIfChanged(homeDirectory, SdkPropsPath, sdkPropsText);
WriteIfChanged(homeDirectory, IntroductionPath, introductionText);
return 0;

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

static async Task<(string ChannelVersion, int Band, Uri ReleasesJsonUrl)?> FindMinimumBandAsync(
    HttpClient http,
    IReadOnlyList<(string ChannelVersion, NuGetVersion LatestSdk, Uri ReleasesJsonUrl)> channels,
    int floorMajor,
    int floorMinor)
{
    // Compiler versions grow monotonically along the band sequence, so the walk goes from the newest band down
    // and stops at the first one whose compiler no longer meets the floor; the band seen just before it is the
    // lowest one that does. Bands are assumed contiguous from 1xx up to the channel's latest SDK.
    //
    // A band that cannot be read ends the walk with null, discarding the bands confirmed so far. Those are the
    // bands nearest the top, and the answer is the band furthest down, so a partial walk answers too high a band
    // rather than an approximate one. Every null return says on stderr what stopped the walk.
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
                Console.Error.WriteLine(
                    $"Cannot read the compiler version of .NET SDK {channel.ChannelVersion}.{band}xx: {exception.Message}");
                return null;
            }

            if (compilerVersion is null)
            {
                Console.Error.WriteLine($"No release branch or compiler pin found for .NET SDK {channel.ChannelVersion}.{band}xx.");
                return null;
            }

            if ((compilerVersion.Major, compilerVersion.Minor).CompareTo((floorMajor, floorMinor)) < 0)
            {
                if (candidate is null)
                {
                    Console.Error.WriteLine($"No .NET SDK feature band ships a compiler at least {floorMajor}.{floorMinor}.");
                }

                return candidate;
            }

            candidate = (channel.ChannelVersion, band, channel.ReleasesJsonUrl);
        }
    }

    if (candidate is null)
    {
        Console.Error.WriteLine("The .NET release index yielded no feature band to examine.");
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

static bool TrySpliceProperty(
    ref string text,
    string path,
    string propertyName,
    string expectedValue,
    ref int staleCount)
{
    var currentValue = XDocument.Parse(text).Descendants(propertyName).FirstOrDefault()?.Value;
    if (currentValue is null)
    {
        Console.Error.WriteLine($"Property {propertyName} not found in {path}.");
        return false;
    }

    if (string.Equals(currentValue, expectedValue, StringComparison.Ordinal))
    {
        return true;
    }

    Console.WriteLine($"{propertyName}: {currentValue} -> {expectedValue}");
    staleCount++;
    var oldElement = $"<{propertyName}>{currentValue}</{propertyName}>";
    var newElement = $"<{propertyName}>{expectedValue}</{propertyName}>";
    if (TryReplaceOnce(ref text, oldElement, newElement))
    {
        return true;
    }

    Console.Error.WriteLine($"Cannot locate {oldElement} in {path}.");
    return false;
}

// The region body is a blank line, the table, and a blank line, so that the table is surrounded by blank lines
// as markdownlint's MD058 wants, whatever the prose around the markers.
static bool TrySpliceRegion(
    ref string text,
    string path,
    string regionName,
    string[] expectedLines,
    ref int staleCount)
{
    var startMarker = $"<!-- {regionName}:START -->";
    var endMarker = $"<!-- {regionName}:END -->";
    var start = IndexOfOnce(text, startMarker);
    var end = IndexOfOnce(text, endMarker);
    if (start < 0 || end < start)
    {
        Console.Error.WriteLine($"Region {regionName} of {path} needs one start marker, and one end marker after it.");
        return false;
    }

    var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
    var expectedBody = newline + newline + string.Join(newline, expectedLines) + newline + newline;
    var bodyStart = start + startMarker.Length;
    if (text.AsSpan(bodyStart, end - bodyStart).SequenceEqual(expectedBody))
    {
        return true;
    }

    Console.WriteLine($"Region {regionName} of {path}: stale");
    staleCount++;
    text = string.Concat(text.AsSpan(0, bodyStart), expectedBody, text.AsSpan(end));
    return true;
}

static void WriteIfChanged(string homeDirectory, string path, string text)
{
    var fullPath = Path.Combine(homeDirectory, path);
    if (!string.Equals(File.ReadAllText(fullPath), text, StringComparison.Ordinal))
    {
        File.WriteAllText(fullPath, text);
    }
}

static bool TryReplaceOnce(ref string text, string oldValue, string newValue)
{
    var start = IndexOfOnce(text, oldValue);
    if (start < 0)
    {
        return false;
    }

    text = string.Concat(text.AsSpan(0, start), newValue, text.AsSpan(start + oldValue.Length));
    return true;
}

// The index of the one occurrence of value in text, or -1 when there is none, or more than one.
static int IndexOfOnce(string text, string value)
{
    var start = text.IndexOf(value, StringComparison.Ordinal);
    if (start < 0)
    {
        return -1;
    }

    return text.IndexOf(value, start + value.Length, StringComparison.Ordinal) >= 0 ? -1 : start;
}

// A pipe table in the "aligned" style of markdownlint's MD060: every cell padded to the width of its column.
static string[] FormatTable(string[][] rows)
{
    var widths = new int[rows[0].Length];
    foreach (var row in rows)
    {
        for (var column = 0; column < widths.Length; column++)
        {
            widths[column] = Math.Max(widths[column], row[column].Length);
        }
    }

    var separator = widths.Select(static width => new string('-', width)).ToArray();
    return [FormatRow(rows[0], widths), FormatRow(separator, widths), .. rows.Skip(1).Select(row => FormatRow(row, widths))];
}

static string FormatRow(string[] cells, int[] widths)
    => "| " + string.Join(" | ", cells.Select((cell, column) => cell.PadRight(widths[column]))) + " |";

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
