// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Process;
using Buildvana.Runtime;
using Buildvana.Tool.Utilities;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Reads the pins of the additional package groups a repository declares: the files it states package
/// versions in beyond the ones the <c>packages</c> scope finds by itself.
/// </summary>
/// <remarks>
/// <para>A group's files need not be imported by any project — Buildvana's own
/// <c>src/Buildvana.Sdk/Sdk/PackageVersions.props</c> is imported by the SDK it ships, not by this
/// repository — so each file is evaluated on its own rather than through the solution. Evaluation gives
/// conditions, properties and metadata the meaning they have everywhere else, and what a group's items
/// bring in from outside its own glob is not the group's.</para>
/// <para>A file matched by two groups belongs to the first group that names it, in configuration order, so
/// that one pin is never reported twice under two captions.</para>
/// </remarks>
internal sealed class AdditionalGroupPinReader(
    IHomeDirectoryProvider home,
    BuildvanaConfig config,
    IProcessRunner processRunner,
    IReporter reporter)
{
    /// <summary>
    /// Evaluates the files of every declared group and reads the pins they state.
    /// </summary>
    /// <param name="cancellationToken">A token that, when signalled, terminates the spawned process.</param>
    /// <returns>The pins found, group by group in configuration order.</returns>
    /// <exception cref="BuildFailedException">A file could not be evaluated or read.</exception>
    public async Task<IReadOnlyList<DependencyPin>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var groups = config.Dependencies.AdditionalPackages;
        if (groups.Count == 0)
        {
            return [];
        }

        var patterns = new List<PathPatternSet>();
        foreach (var group in groups)
        {
            patterns.Add(PathPatternSet.Parse([group.Files]));
        }

        var pins = new List<DependencyPin>();
        var index = new PinDeclarationIndex(home, [.. Items(groups)]);
        foreach (var relativePath in RepositoryFiles.CreateFinder(home).GetFiles())
        {
            var groupIndex = patterns.FindIndex(pattern => pattern.Contains(relativePath));
            if (groupIndex >= 0)
            {
                await ReadFileAsync(pins, index, groups[groupIndex], patterns[groupIndex], relativePath, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return pins;
    }

    private static IEnumerable<string> Items(IReadOnlyList<AdditionalPackagesConfig> groups)
    {
        foreach (var group in groups)
        {
            yield return group.Items;
        }
    }

    // MSBuild answers -getItem with a JSON object holding one array per item name asked for, each element
    // an object of the item's metadata plus its Identity.
    private static JsonArray? ReadItems(string output, string itemName, string path)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(output);
        }
        catch (JsonException e)
        {
            throw new BuildFailedException(ExitCodes.ExternalProgramFailed, $"Could not read the items of '{path}': {e.Message}", e);
        }

        return root?["Items"]?[itemName] as JsonArray;
    }

    // Every value MSBuild answers with is read the way EvaluatedMetadata prescribes, whether it is an item's
    // identity, the file that declares it, its version, or the policy it states for itself.
    private static string? ReadMetadata(JsonNode? item, string name)
        => item?[name] is JsonValue value && value.TryGetValue<string>(out var text) ? EvaluatedMetadata.Stated(text) : null;

    private async Task ReadFileAsync(
        List<DependencyPin> pins,
        PinDeclarationIndex index,
        AdditionalPackagesConfig group,
        PathPatternSet pattern,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var output = await EvaluateAsync(relativePath, group.Items, cancellationToken).ConfigureAwait(false);
        foreach (var item in ReadItems(output, group.Items, relativePath) ?? [])
        {
            if (ReadPin(index, group, pattern, item) is { } pin)
            {
                pins.Add(pin);
            }
        }
    }

    private DependencyPin? ReadPin(PinDeclarationIndex index, AdditionalPackagesConfig group, PathPatternSet pattern, JsonNode? item)
    {
        var id = ReadMetadata(item, "Identity");
        var versionText = ReadMetadata(item, "Version");
        if (id is null || versionText is null || BuildvanaFamily.Contains(id))
        {
            return null;
        }

        // An item an import brought in from outside the group's glob belongs to whatever declares it, and
        // the group's policy has nothing to say about it.
        var declaringFile = ReadMetadata(item, "DefiningProjectFullPath");
        if (declaringFile is null
            || !home.TryGetRelativePath(declaringFile, out var declaringRelativePath)
            || !pattern.Contains(declaringRelativePath))
        {
            return null;
        }

        var pin = DependencyPin.Create(DependencyScope.Packages, id, versionText, declaringRelativePath) with
        {
            ItemType = group.Items,
            MetadataPolicy = ReadMetadata(item, "UpdatePolicy"),
            GroupCaption = group.Caption,
        };

        var statesVersion = index.StatesVersion(declaringRelativePath, group.Items, id, versionText);
        return pin.Management == PinManagement.Managed && !statesVersion
            ? pin with { Management = PinManagement.IndirectVersion }
            : pin;
    }

    private async Task<string> EvaluateAsync(string relativePath, string itemName, CancellationToken cancellationToken)
    {
        reporter.Detail($"Reading the '{itemName}' items of '{relativePath}'...");
        string[] args = ["msbuild", home.GetFullPath(relativePath), "-nologo", $"-getItem:{itemName}"];
        var result = await processRunner.RunAsync(
            DotNetMuxer.Path,
            args,
            throwOnNonZero: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.ExitCode == 0)
        {
            return result.StandardOutput;
        }

        foreach (var line in result.StandardOutput.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            reporter.ChildError(line, Verbosity.Quiet);
        }

        throw new BuildFailedException(
            ExitCodes.ExternalProgramFailed,
            $"MSBuild could not evaluate '{relativePath}' (exit code {result.ExitCode}).");
    }
}
