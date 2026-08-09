// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.Configuration;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.IO;
using Buildvana.Core.Json;
using Buildvana.Core.Process;
using Buildvana.Runtime;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;
using NuGet.Versioning;

namespace Buildvana.Tool.Services;

/// <summary>
/// Compares bv's own version with the repository's pinned Buildvana SDK version (the <c>Buildvana.Sdk</c> entry
/// under <c>msbuild-sdks</c> in <c>global.json</c>), and updates the repository's Buildvana pins on request.
/// </summary>
/// <remarks>
/// <para>bv, Buildvana SDK, and Buildvana.Runtime are released in lockstep and designed as a matched group;
/// a version mismatch between the running bv and the pinned SDK produces silent behavior drift (e.g. hooks
/// deserializing configuration with a different Buildvana.Runtime than the bv that validated it). Commands
/// whose registration declares <c>usesSdk</c> therefore refuse to run on a mismatch (see
/// <see cref="EnsureSdkVersionMatch"/>), unless the check is skipped with <c>--skip-sdk-check</c>.</para>
/// <para>Versions are compared by SemVer precedence, prerelease included (so <c>-preview</c> sorts before
/// stable); build metadata never participates in comparisons and is never written to pins.</para>
/// </remarks>
internal sealed partial class SelfVersionService
{
    private const string GlobalJsonFileName = "global.json";
    private const string MsbuildSdksPropertyName = "msbuild-sdks";
    private const string SdkPackageId = "Buildvana.Sdk";
    private const string ToolPackageId = ToolManifest.BvPackageId;
    private const string SchemaPropertyName = "$schema";

    private readonly IReporter _reporter;
    private readonly IHomeDirectoryProvider _home;
    private readonly IJsonHelper _jsonHelper;
    private readonly IProcessRunner _processRunner;
    private readonly NuGetVersion _ownVersion;

    /// <summary>
    /// Initializes a new instance of the <see cref="SelfVersionService"/> class.
    /// </summary>
    /// <param name="reporter">The reporter to log to.</param>
    /// <param name="home">The provider of the home directory, where <c>global.json</c> and the tool manifest live.</param>
    /// <param name="jsonHelper">The JSON helper used to read and rewrite pins.</param>
    /// <param name="processRunner">The process runner used to invoke <c>dotnet tool update</c>.</param>
    /// <param name="ownVersion">The version of the running bv.</param>
    public SelfVersionService(
        IReporter reporter,
        IHomeDirectoryProvider home,
        IJsonHelper jsonHelper,
        IProcessRunner processRunner,
        NuGetVersion ownVersion)
    {
        Guard.IsNotNull(reporter);
        Guard.IsNotNull(home);
        Guard.IsNotNull(jsonHelper);
        Guard.IsNotNull(processRunner);
        Guard.IsNotNull(ownVersion);
        _reporter = reporter;
        _home = home;
        _jsonHelper = jsonHelper;
        _processRunner = processRunner;
        _ownVersion = ownVersion;
    }

    // The well-known shape of the configuration file's schema reference: the version segment between the
    // repository slug and the schema path is the only part the update rewrites. Anything else is left alone.
    // The dogfooding post-release hook (.buildvana/hooks/release/post-release.cs) applies the same expression to the
    // same file when a release moves this repository's own self-references; keep the two copies identical.
    [GeneratedRegex("(Tenacom/Buildvana/)[^/]+(/schemas/)", RegexOptions.CultureInvariant)]
    private static partial Regex SchemaUrlRegex { get; }

    private string GlobalJsonPath => _home.GetFullPath(GlobalJsonFileName);

    private string OwnVersionText => _ownVersion.ToNormalizedString();

    /// <summary>
    /// Ensures that the repository's pinned Buildvana SDK version matches this bv's version.
    /// </summary>
    /// <exception cref="BuildFailedException">The check failed — e.g. <c>global.json</c> could not be read,
    /// does not pin the SDK version, or pins a version different from this bv's; the message names the
    /// failure.</exception>
    public void EnsureSdkVersionMatch()
    {
        var (pin, missingReason) = ReadPin();
        if (pin is null)
        {
            throw new BuildFailedException(
                $"SDK version check failed: {missingReason}. This bv is version {OwnVersionText}. "
                + $"Run 'bv update' to pin {SdkPackageId} {OwnVersionText}, or pass --skip-sdk-check to skip this check.");
        }

        if (!NuGetVersion.TryParse(pin, out var pinnedVersion))
        {
            throw new BuildFailedException(
                $"SDK version check failed: the {SdkPackageId} version pinned in {GlobalJsonFileName} ('{pin}') is not a valid version. "
                + $"This bv is version {OwnVersionText}. Run 'bv update' to repin {SdkPackageId} {OwnVersionText}, "
                + "or pass --skip-sdk-check to skip this check.");
        }

        if (!VersionComparer.VersionRelease.Equals(pinnedVersion, _ownVersion))
        {
            throw new BuildFailedException(
                $"SDK version check failed: {GlobalJsonFileName} pins {SdkPackageId} {pinnedVersion.ToNormalizedString()}, "
                + $"but this bv is version {OwnVersionText}. Run 'bv update' to update this repository's pins "
                + "to a single version, or pass --skip-sdk-check to skip this check.");
        }

        _reporter.Detail($"SDK version check passed: {GlobalJsonFileName} pins {SdkPackageId} {pin}.");
    }

    /// <summary>
    /// Updates the repository's Buildvana pins — the bv entry in the tool manifest, the Buildvana SDK entry in
    /// <c>global.json</c>, and the configuration file's schema reference — to this bv's version.
    /// </summary>
    /// <remarks>
    /// <para>The tool manifest is updated through <c>dotnet tool update</c> (or <c>dotnet tool install
    /// --create-manifest-if-needed</c> when there is no bv entry yet), which also downloads the version so the
    /// next <c>dotnet bv</c> invocation can run it. The <c>global.json</c> pin is rewritten in place, creating
    /// the file or the <c>msbuild-sdks</c> section as needed. The configuration file's schema reference is
    /// rewritten only when it matches the well-known <c>Tenacom/Buildvana/&lt;version&gt;/schemas/</c> URL shape;
    /// afterwards the configuration file is loaded against this version's model, and any problems are reported
    /// as warnings — the file keeps working for the commands that do not read it, and the user decides how to
    /// migrate it.</para>
    /// <para>When an existing pin is newer than this bv, the update would be a downgrade and fails unless
    /// <paramref name="force"/> is <see langword="true"/>.</para>
    /// </remarks>
    /// <param name="force">Whether to update even when an existing pin is newer than this bv.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the ongoing operation.</param>
    /// <returns>The per-target summary of what changed, for the command to print.</returns>
    /// <exception cref="BuildFailedException">The update failed — e.g. a file could not be read or written,
    /// <c>dotnet tool update</c> failed, or an existing pin is newer than this bv and <paramref name="force"/>
    /// is <see langword="false"/>; the message names the failure.</exception>
    public async Task<UpdateSummary> UpdateRepositoryAsync(bool force, CancellationToken cancellationToken = default)
    {
        var manifestPin = ToolManifest.ReadBvPin(_jsonHelper, _home.HomeDirectory);
        EnsureUsableManifestEntry(manifestPin);
        var (sdkPinText, _) = ReadPin();
        NuGetVersion? sdkPin = null;
        if (sdkPinText is not null && NuGetVersion.TryParse(sdkPinText, out var parsedSdkPin))
        {
            sdkPin = parsedSdkPin;
        }

        EnsureNoUnforcedDowngrade(manifestPin.Version, sdkPin, force);

        // Manifest first: pinning it spawns the dotnet CLI, the one step with an external actor, so a failure
        // there leaves the repository untouched. A failed file write after it leaves the manifest already
        // pinned — a state a rerun reads as "unchanged" before retrying the writes, so the window self-heals.
        var toolManifestLine = await PinToolManifestAsync(manifestPin, cancellationToken).ConfigureAwait(false);
        var globalJsonLine = UpdateGlobalJson(sdkPinText, sdkPin);
        var configFileLine = UpdateConfigSchemaReference();
        return new UpdateSummary(toolManifestLine, globalJsonLine, configFileLine);
    }

    // The dotnet CLI reads the manifest with the same version parser bv uses, so an entry whose version bv
    // cannot parse is an entry no `dotnet tool` verb can rewrite either — the CLI fails reading the manifest
    // before it gets to the entry. The only way forward is fixing the file by hand, so say so up front instead
    // of forwarding a confusing CLI error.
    private static void EnsureUsableManifestEntry(BvManifestPin manifestPin)
    {
        if (!manifestPin.HasEntry || manifestPin.Version is not null)
        {
            return;
        }

        var problem = manifestPin.VersionText is null
            ? "has no version"
            : $"pins version '{manifestPin.VersionText}', which is not a valid version";
        throw new BuildFailedException(
            $"Cannot update this repository: the {ToolPackageId} entry in {ToolManifest.RelativePath} {problem}. "
            + $"Fix or remove the entry, then run '{ToolPackageId} update' again.");
    }

    private static void CreateGlobalJson(string path, string version)
    {
        var content = $$"""
            {
              "{{MsbuildSdksPropertyName}}": {
                "{{SdkPackageId}}": "{{version}}"
              }
            }
            """;

        // Normalize to LF + a single trailing newline: a raw string literal carries the source file's own
        // line endings, and the written file must not depend on how this file was checked out.
        UserFile.WriteAllText(path, content.ReplaceLineEndings("\n") + "\n");
    }

    private static bool IsPinPath(IReadOnlyList<string> propertyPath)
        => propertyPath is [MsbuildSdksPropertyName, SdkPackageId];

    // The update never downgrades silently: an old bv run by habit in a newer repository must not roll the
    // repository back. `dotnet bv update` runs the repository's own pinned bv (the update command is exempt
    // from delegation, so a plain `bv update` runs the invoked binary), and --force covers the deliberate
    // downgrade (e.g. bisecting a regression). The guard deliberately covers the two version pins only: the
    // $schema reference is cosmetic metadata, and when either pin is newer the update throws right here,
    // before the schema reference is ever touched.
    private void EnsureNoUnforcedDowngrade(NuGetVersion? manifestPin, NuGetVersion? sdkPin, bool force)
    {
        if (force)
        {
            return;
        }

        var newerPins = new List<string>();
        if (manifestPin is not null && VersionComparer.VersionRelease.Compare(manifestPin, _ownVersion) > 0)
        {
            newerPins.Add($"the tool manifest pins {ToolPackageId} {manifestPin.ToNormalizedString()}");
        }

        if (sdkPin is not null && VersionComparer.VersionRelease.Compare(sdkPin, _ownVersion) > 0)
        {
            newerPins.Add($"{GlobalJsonFileName} pins {SdkPackageId} {sdkPin.ToNormalizedString()}");
        }

        if (newerPins.Count == 0)
        {
            return;
        }

        throw new BuildFailedException(
            $"This bv is version {OwnVersionText}, but {string.Join(" and ", newerPins)}: updating would be a downgrade. "
            + $"Run 'dotnet {ToolPackageId} update' to update the repository to its own pinned {ToolPackageId}, "
            + $"or pass --force to downgrade to {OwnVersionText}.");
    }

    // Pins bv's own version in the tool manifest through the dotnet CLI, which rewrites the manifest and
    // downloads the version in one go — hand-editing the manifest would leave the pin unrestored. The choice
    // between update and install is keyed on entry presence, mirroring the CLI's own contract: update rewrites
    // an existing entry, and install creates the manifest itself when the repository has none. (An entry with
    // an unusable version never reaches this point; EnsureUsableManifestEntry rejects it up front.)
    private async Task<string> PinToolManifestAsync(BvManifestPin manifestPin, CancellationToken cancellationToken)
    {
        var currentPin = manifestPin.Version;
        if (currentPin is not null && VersionComparer.VersionRelease.Equals(currentPin, _ownVersion))
        {
            return $"{ToolPackageId}: {OwnVersionText} (tool manifest, unchanged)";
        }

        var hasEntry = manifestPin.HasEntry;

        // The dotnet CLI has a downgrade guard of its own: `tool update` refuses to move a tool to a lower
        // version unless --allow-downgrade is passed. A downgrade only reaches this point forced (see
        // EnsureNoUnforcedDowngrade), so pass the flag exactly when bv has itself authorized the downgrade,
        // leaving the CLI's guard armed on every other path.
        var isDowngrade = currentPin is not null && VersionComparer.VersionRelease.Compare(currentPin, _ownVersion) > 0;
        string[] args = isDowngrade ? ["tool", "update", ToolPackageId, "--version", OwnVersionText, "--allow-downgrade"]
            : hasEntry ? ["tool", "update", ToolPackageId, "--version", OwnVersionText]
            : ["tool", "install", ToolPackageId, "--version", OwnVersionText, "--create-manifest-if-needed"];
        _ = await _processRunner.RunAsync(
            DotNetMuxer.Path,
            args,
            workingDirectory: _home.HomeDirectory,
            onStdout: line => _reporter.ChildOutput(line, null),
            onStderr: line => _reporter.ChildError(line, null),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return hasEntry
            ? $"{ToolPackageId}: {currentPin!.ToNormalizedString()} -> {OwnVersionText} (tool manifest)"
            : $"{ToolPackageId}: {OwnVersionText} (tool manifest, added)";
    }

    private string UpdateGlobalJson(string? currentPinText, NuGetVersion? currentPin)
    {
        if (currentPin is not null && VersionComparer.VersionRelease.Equals(currentPin, _ownVersion))
        {
            return $"{SdkPackageId}: {OwnVersionText} ({GlobalJsonFileName}, unchanged)";
        }

        WritePin(OwnVersionText);
        return currentPinText is not null
            ? $"{SdkPackageId}: {currentPinText} -> {OwnVersionText} ({GlobalJsonFileName})"
            : $"{SdkPackageId}: {OwnVersionText} ({GlobalJsonFileName}, added)";
    }

    // Rewrites the version segment of the configuration file's $schema URL in place, when the URL has the
    // well-known shape; a hand-rolled or absent reference is reported, not touched. Runs on whichever of the
    // four candidate locations holds the configuration file; no file at all means nothing to update or validate.
    private string? UpdateConfigSchemaReference()
    {
        string? path;
        try
        {
            path = BuildvanaConfig.FindFile(_home.HomeDirectory);
        }
        catch (BuildvanaRuntimeException e)
        {
            throw new BuildFailedException(e.Message, e);
        }

        if (path is null)
        {
            return null;
        }

        var fileName = Path.GetFileName(path);
        string? schemaReference = null;
        var changed = _jsonHelper.RewriteStringValues(path, (propertyPath, value) =>
        {
            if (propertyPath.Count != 1 || propertyPath[0] != SchemaPropertyName)
            {
                return null;
            }

            schemaReference = value;
            var rewritten = SchemaUrlRegex.Replace(value, m => m.Groups[1].Value + OwnVersionText + m.Groups[2].Value);
            return string.Equals(rewritten, value, StringComparison.Ordinal) ? null : rewritten;
        });

        var line = schemaReference is null ? $"{fileName}: no schema reference found"
            : changed ? $"{fileName}: schema reference updated"
            : SchemaUrlRegex.IsMatch(schemaReference) ? $"{fileName}: schema reference unchanged"
            : $"{fileName}: schema reference not recognized, left unchanged";
        ReportConfigValidationProblems(fileName);
        return line;
    }

    // The configuration file's content may predate this bv's model; loading it with this version's validating
    // loader turns the drift into actionable diagnostics. Problems are warnings, not errors: the file keeps
    // working for the commands that do not read it, and the user decides how to migrate it.
    private void ReportConfigValidationProblems(string fileName)
    {
        try
        {
            _ = BuildvanaConfigLoader.Load(_home.HomeDirectory);
        }
        catch (BuildFailedException e)
        {
            _reporter.Warning($"{fileName} does not load cleanly with this bv version: {e.Message}");
            foreach (var diagnostic in e.Diagnostics)
            {
                _reporter.ChildError(diagnostic.ToString(), Verbosity.Quiet);
            }
        }
    }

    // Reads the pinned SDK version from global.json. A null version comes with a reason phrase for messages;
    // note that the returned version, when present, is not guaranteed to parse as a NuGetVersion.
    private (string? Version, string MissingReason) ReadPin()
    {
        var path = GlobalJsonPath;
        if (!File.Exists(path))
        {
            return (null, $"there is no {GlobalJsonFileName} in {_home.HomeDirectory}");
        }

        var root = _jsonHelper.LoadObject(path);
        if (!root.TryGetPropertyValue(MsbuildSdksPropertyName, out var sdksNode) || sdksNode is not JsonObject sdks)
        {
            return (null, $"{GlobalJsonFileName} has no '{MsbuildSdksPropertyName}' section");
        }

        string? pin = null;
        var hasPin = sdks.TryGetPropertyValue(SdkPackageId, out var pinNode)
            && pinNode is JsonValue pinValue
            && pinValue.TryGetValue(out pin);
        return hasPin
            ? (pin, string.Empty)
            : (null, $"{GlobalJsonFileName} does not pin a {SdkPackageId} version under '{MsbuildSdksPropertyName}'");
    }

    // Rewrites the global.json pin to the given version, creating the file, the msbuild-sdks section, or the
    // Buildvana.Sdk entry as needed.
    private void WritePin(string version)
    {
        var path = GlobalJsonPath;
        if (!File.Exists(path))
        {
            CreateGlobalJson(path, version);
            _reporter.Detail($"Created {GlobalJsonFileName}, pinning {SdkPackageId} {version}.");
            return;
        }

        var root = _jsonHelper.LoadObject(path);
        var hasSdksSection = root.TryGetPropertyValue(MsbuildSdksPropertyName, out var sdksNode) && sdksNode is JsonObject;
        if (!hasSdksSection)
        {
            var section = new JsonObject { [SdkPackageId] = version };
            var inserted = _jsonHelper.InsertProperty(path, [], MsbuildSdksPropertyName, section);
            BuildFailedException.ThrowIfNot(inserted, $"{path} has a '{MsbuildSdksPropertyName}' property that is not an object.");
            _reporter.Detail($"Added a '{MsbuildSdksPropertyName}' section to {GlobalJsonFileName}, pinning {SdkPackageId} {version}.");
            return;
        }

        if (((JsonObject)sdksNode!).ContainsKey(SdkPackageId))
        {
            var rewritten = _jsonHelper.RewriteStringValues(path, (propertyPath, _) => IsPinPath(propertyPath) ? version : null);
            BuildFailedException.ThrowIfNot(rewritten, $"{path} has a '{MsbuildSdksPropertyName}.{SdkPackageId}' property that is not a string.");
            _reporter.Detail($"Updated {GlobalJsonFileName}: {SdkPackageId} pinned to {version}.");
            return;
        }

        _ = _jsonHelper.InsertProperty(path, [MsbuildSdksPropertyName], SdkPackageId, JsonValue.Create(version));
        _reporter.Detail($"Added a {SdkPackageId} {version} pin to {GlobalJsonFileName}.");
    }
}
