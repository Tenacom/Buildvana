// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Json;
using Buildvana.Core.Process;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;
using NuGet.Versioning;

namespace Buildvana.Tool.Services;

/// <summary>
/// Compares bv's own version with the repository's pinned Buildvana SDK version (the <c>Buildvana.Sdk</c> entry
/// under <c>msbuild-sdks</c> in <c>global.json</c>), and aligns the two on request.
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
internal sealed class SelfVersionService
{
    private const string GlobalJsonFileName = "global.json";
    private const string MsbuildSdksPropertyName = "msbuild-sdks";
    private const string SdkPackageId = "Buildvana.Sdk";
    private const string ToolPackageId = "bv";

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

    private string GlobalJsonPath => Path.Combine(_home.HomeDirectory, GlobalJsonFileName);

    private string ToolManifestPath => Path.Combine(_home.HomeDirectory, ".config", "dotnet-tools.json");

    private string OwnVersionText => _ownVersion.ToNormalizedString();

    /// <summary>
    /// Ensures that the repository's pinned Buildvana SDK version matches this bv's version.
    /// </summary>
    /// <exception cref="BuildFailedException">There is no pin, the pin is not a valid version, or the pinned
    /// version differs from this bv's version.</exception>
    public void EnsureSdkVersionMatch()
    {
        var (pin, missingReason) = ReadPin();
        if (pin is null)
        {
            throw new BuildFailedException(
                $"SDK version check failed: {missingReason}. This bv is version {OwnVersionText}. "
                + $"Run 'bv sync-sdk' to pin {SdkPackageId} {OwnVersionText}, or pass --skip-sdk-check to skip this check.");
        }

        if (!NuGetVersion.TryParse(pin, out var pinnedVersion))
        {
            throw new BuildFailedException(
                $"SDK version check failed: the {SdkPackageId} version pinned in {GlobalJsonFileName} ('{pin}') is not a valid version. "
                + $"This bv is version {OwnVersionText}. Run 'bv sync-sdk' to repin {SdkPackageId} {OwnVersionText}, "
                + "or pass --skip-sdk-check to skip this check.");
        }

        if (!VersionComparer.VersionRelease.Equals(pinnedVersion, _ownVersion))
        {
            throw new BuildFailedException(
                $"SDK version check failed: {GlobalJsonFileName} pins {SdkPackageId} {pinnedVersion.ToNormalizedString()}, "
                + $"but this bv is version {OwnVersionText}. Run 'bv sync-sdk' to align them "
                + "(updating bv with 'dotnet tool update bv' or editing the pin also works), "
                + "or pass --skip-sdk-check to skip this check.");
        }

        _reporter.Detail($"SDK version check passed: {GlobalJsonFileName} pins {SdkPackageId} {pin}.");
    }

    /// <summary>
    /// Aligns the repository's pinned Buildvana SDK version with this bv's version, updating whichever is older.
    /// </summary>
    /// <remarks>
    /// <para>A missing, or unparseable, pin counts as older than any version. When the pin is older than (or
    /// equal to except for build metadata) this bv, the pin is rewritten; when the pin is newer, bv itself is
    /// updated via <c>dotnet tool update</c> — but only when the running bv comes from the repository's tool
    /// manifest, so that e.g. a <c>dnx bv</c> invocation never touches <c>dotnet-tools.json</c>.</para>
    /// <para>Whenever the pin ends up matching this bv but the tool manifest still pins a different bv (e.g.
    /// a newer globally-installed bv synced a repository whose manifest pins the older one), a warning points
    /// out that the manifest is not aligned yet and how to finish the job.</para>
    /// </remarks>
    /// <param name="cancellationToken">A token that, when signalled, terminates the ongoing operation.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    /// <exception cref="BuildFailedException">The pin is newer than this bv, but bv cannot be updated from the
    /// repository's tool manifest.</exception>
    public async Task SyncSdkAsync(CancellationToken cancellationToken = default)
    {
        var (pin, _) = ReadPin();
        NuGetVersion? pinnedVersion = null;
        if (pin is not null && NuGetVersion.TryParse(pin, out var parsed))
        {
            pinnedVersion = parsed;
        }

        if (pinnedVersion is not null && VersionComparer.VersionRelease.Equals(pinnedVersion, _ownVersion))
        {
            _reporter.Info($"{SdkPackageId} {pin} and bv {OwnVersionText} are already in sync.");
            WarnIfToolManifestDisagrees();
            return;
        }

        var pinIsNewer = pinnedVersion is not null && VersionComparer.VersionRelease.Compare(pinnedVersion, _ownVersion) > 0;
        if (pinIsNewer)
        {
            await UpdateToolManifestAsync(pinnedVersion!, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            WritePin(OwnVersionText);
            WarnIfToolManifestDisagrees();
        }
    }

    private static void CreateGlobalJson(string path, string version)
    {
        string[] lines =
        [
            "{",
            $"  \"{MsbuildSdksPropertyName}\": {{",
            $"    \"{SdkPackageId}\": \"{version}\"",
            "  }",
            "}",
            string.Empty,
        ];
        File.WriteAllText(path, string.Join('\n', lines));
    }

    private static bool IsPinPath(IReadOnlyList<string> propertyPath)
        => propertyPath.Count == 2 && propertyPath[0] == MsbuildSdksPropertyName && propertyPath[1] == SdkPackageId;

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
            _reporter.Info($"Created {GlobalJsonFileName}, pinning {SdkPackageId} {version}.");
            return;
        }

        var root = _jsonHelper.LoadObject(path);
        var hasSdksSection = root.TryGetPropertyValue(MsbuildSdksPropertyName, out var sdksNode) && sdksNode is JsonObject;
        if (!hasSdksSection)
        {
            var section = new JsonObject { [SdkPackageId] = version };
            var inserted = _jsonHelper.InsertProperty(path, [], MsbuildSdksPropertyName, section);
            BuildFailedException.ThrowIfNot(inserted, $"{path} has a '{MsbuildSdksPropertyName}' property that is not an object.");
            _reporter.Info($"Added a '{MsbuildSdksPropertyName}' section to {GlobalJsonFileName}, pinning {SdkPackageId} {version}.");
            return;
        }

        if (((JsonObject)sdksNode!).ContainsKey(SdkPackageId))
        {
            var rewritten = _jsonHelper.RewriteStringValues(path, (propertyPath, _) => IsPinPath(propertyPath) ? version : null);
            BuildFailedException.ThrowIfNot(rewritten, $"{path} has a '{MsbuildSdksPropertyName}.{SdkPackageId}' property that is not a string.");
            _reporter.Info($"Updated {GlobalJsonFileName}: {SdkPackageId} pinned to {version}.");
            return;
        }

        _ = _jsonHelper.InsertProperty(path, [MsbuildSdksPropertyName], SdkPackageId, JsonValue.Create(version));
        _reporter.Info($"Added a {SdkPackageId} {version} pin to {GlobalJsonFileName}.");
    }

    // A heads-up for the half-synced state: the pin now matches this bv, but the tool manifest still pins a
    // different bv, so the next `dotnet bv` invocation will run that version and fail the SDK version check.
    private void WarnIfToolManifestDisagrees()
    {
        var manifestVersion = ReadToolManifestPin();
        if (manifestVersion is null || VersionComparer.VersionRelease.Equals(manifestVersion, _ownVersion))
        {
            return;
        }

        _reporter.Warning(
            $"The tool manifest (.config/dotnet-tools.json) still pins {ToolPackageId} {manifestVersion.ToNormalizedString()}, "
            + $"so the next 'dotnet {ToolPackageId}' invocation will run that version and fail the SDK version check. "
            + $"Run 'dotnet tool update {ToolPackageId} --version {OwnVersionText}', or re-run sync-sdk through the manifest "
            + $"('dotnet {ToolPackageId} sync-sdk'), to align it.");
    }

    // Updates bv itself to the pinned SDK version via `dotnet tool update`, which rewrites the tool manifest
    // and downloads the new version in one go. Guarded on the manifest pinning the running bv's version, per
    // the contract described in SyncSdkAsync's remarks.
    private async Task UpdateToolManifestAsync(NuGetVersion pinnedVersion, CancellationToken cancellationToken)
    {
        var target = pinnedVersion.ToNormalizedString();
        var mismatchPreamble = $"The pinned {SdkPackageId} version ({target}) is newer than this bv ({OwnVersionText})";
        var manualFixHint = $"update the bv you are running yourself (e.g. 'dotnet tool update -g {ToolPackageId}' for a global install), then retry";
        if (!File.Exists(ToolManifestPath))
        {
            throw new BuildFailedException($"{mismatchPreamble}, but this repository has no tool manifest (.config/dotnet-tools.json); {manualFixHint}.");
        }

        var manifestVersion = ReadToolManifestPin();
        var runningBvComesFromManifest = manifestVersion is not null
            && VersionComparer.VersionRelease.Equals(manifestVersion, _ownVersion);
        if (!runningBvComesFromManifest)
        {
            throw new BuildFailedException(
                $"{mismatchPreamble}, but the running bv does not come from this repository's tool manifest, "
                + $"which will therefore not be touched; {manualFixHint}.");
        }

        _reporter.Info($"Updating {ToolPackageId} to {target} via 'dotnet tool update'...");
        _ = await _processRunner.RunAsync(
            DotNetMuxer.Path,
            ["tool", "update", ToolPackageId, "--version", target],
            workingDirectory: _home.HomeDirectory,
            onStdout: line => _reporter.ChildOutput(line, null),
            onStderr: line => _reporter.ChildError(line, null),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        _reporter.Info($"{ToolPackageId} updated to {target}. Re-run your command to use the new version.");
    }

    // Reads the bv version pinned in the repository's tool manifest; null when the manifest, the bv entry,
    // or a parseable version is missing.
    private NuGetVersion? ReadToolManifestPin()
    {
        var path = ToolManifestPath;
        if (!File.Exists(path))
        {
            return null;
        }

        var manifest = _jsonHelper.LoadObject(path);
        string? version = null;
        var hasEntry = manifest.TryGetPropertyValue("tools", out var toolsNode)
            && toolsNode is JsonObject tools
            && tools.TryGetPropertyValue(ToolPackageId, out var toolNode)
            && toolNode is JsonObject toolEntry
            && toolEntry.TryGetPropertyValue("version", out var versionNode)
            && versionNode is JsonValue versionValue
            && versionValue.TryGetValue(out version);
        return hasEntry && NuGetVersion.TryParse(version, out var parsed) ? parsed : null;
    }
}
