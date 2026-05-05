// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Buildvana.Core.HomeDirectory;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;
using Louis.Collections;
using Microsoft.Extensions.Logging;

namespace Buildvana.Tool.Services.PublicApiFiles;

/// <summary>
/// Manages pairs of <c>PublicAPI.Shipped.txt</c> and <c>PublicAPI.Unshipped.txt</c> files throughout the repository.
/// </summary>
public sealed class PublicApiFilesService
{
    private const string RemovedPrefix = "*REMOVED*";

    private readonly IHomeDirectoryProvider _home;
    private readonly ILogger<PublicApiFilesService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PublicApiFilesService"/> class.
    /// </summary>
    public PublicApiFilesService(IHomeDirectoryProvider home, ILogger<PublicApiFilesService> logger)
    {
        Guard.IsNotNull(home);
        Guard.IsNotNull(logger);
        _home = home;
        _logger = logger;
    }

    /// <summary>
    /// Gets the kind of change public APIs underwent, according to the presence of new public APIs and/or the removal of existing public APIs
    /// in all <c>PublicAPI.Unshipped.txt</c> files of the repository.
    /// </summary>
    /// <returns>
    /// <para>If at least one public API was removed, <see cref="ApiChangeKind.Breaking"/>.</para>
    /// <para>If no public API was removed, but at least one was added, <see cref="ApiChangeKind.Additive"/>.</para>
    /// <para>If no public API was removed nor added, <see cref="ApiChangeKind.None"/>.</para>
    /// </returns>
    public ApiChangeKind GetApiChangeKind()
    {
        _logger.LogInformation("Computing API change kind according to unshipped public API files...");
        var result = ApiChangeKind.None;
        foreach (var unshippedPath in GetAllPublicApiFilePairs().Select(pair => pair.UnshippedPath))
        {
            var fileResult = GetApiChangeKind(unshippedPath);
            _logger.LogDebug("{UnshippedPath} -> {Result}", unshippedPath, fileResult);
            if (fileResult == ApiChangeKind.Breaking)
            {
                return ApiChangeKind.Breaking;
            }
            else if (fileResult > result)
            {
                result = fileResult;
            }
        }

        return result;
    }

    /// <summary>
    /// Transfers unshipped public API definitions from <c>PublicAPI.Unshipped.txt</c> to <c>PublicAPI.Shipped.txt</c>
    /// in all directories of the repository where both files exist.
    /// </summary>
    /// <returns>An enumeration of the modified files.</returns>
    public IEnumerable<string> TransferAllPublicApisToShipped()
    {
        _logger.LogInformation("Updating public API files...");
        foreach (var (unshippedPath, shippedPath) in GetAllPublicApiFilePairs())
        {
            _logger.LogDebug("Updating {ShippedPath}...", shippedPath);
            if (!TransferPublicApisToShipped(unshippedPath, shippedPath))
            {
                continue;
            }

            yield return shippedPath;
            yield return unshippedPath;
        }
    }

    private static ApiChangeKind GetApiChangeKind(string unshippedPath)
    {
        var unshippedLines = File.ReadAllLines(unshippedPath, Encoding.UTF8);
        static bool IsEmptyOrStartsWithHash(string s) => s.Length == 0 || s[0] == '#';
        var unshippedPublicApiLines = unshippedLines.SkipWhile(IsEmptyOrStartsWithHash);
        var newApiPresent = false;
        foreach (var line in unshippedPublicApiLines)
        {
            if (line.StartsWith(RemovedPrefix, StringComparison.Ordinal))
            {
                return ApiChangeKind.Breaking;
            }

            newApiPresent = true;
        }

        return newApiPresent ? ApiChangeKind.Additive : ApiChangeKind.None;
    }

    private static bool TransferPublicApisToShipped(string unshippedPath, string shippedPath)
    {
        var utf8 = new UTF8Encoding(false);
        var unshippedLines = File.ReadAllLines(unshippedPath, utf8);
        var unshippedHeaderLines = unshippedLines.TakeWhile(IsEmptyOrStartsWithHash).ToArray();
        if (unshippedHeaderLines.Length == unshippedLines.Length)
        {
            return false;
        }

        var shippedLines = File.ReadAllLines(shippedPath, utf8);
        var shippedHeaderLines = shippedLines.TakeWhile(IsEmptyOrStartsWithHash).ToArray();

        var removedLines = unshippedLines
            .Skip(unshippedHeaderLines.Length)
            .Where(StartsWithRemovedPrefix)
            .Select(static l => l[RemovedPrefix.Length..])
            .OrderBy(static l => l, StringComparer.Ordinal) // For BinarySearch
            .ToArray();

        var newShippedLines = shippedLines
            .Skip(shippedHeaderLines.Length)
            .Where(x => IsNotPresent(removedLines, x))
            .Concat(unshippedLines
                .Skip(unshippedHeaderLines.Length)
                .Where(DoesNotStartWithRemovedPrefix))
            .OrderBy(static l => l, StringComparer.Ordinal);

        File.WriteAllLines(shippedPath, shippedHeaderLines.Concat(newShippedLines), utf8);
        File.WriteAllLines(unshippedPath, unshippedHeaderLines, utf8);
        return true;

        static bool IsEmptyOrStartsWithHash(string s) => s.Length == 0 || s[0] == '#';
        static bool StartsWithRemovedPrefix(string s) => s.StartsWith(RemovedPrefix, StringComparison.Ordinal);
        static bool DoesNotStartWithRemovedPrefix(string s) => !StartsWithRemovedPrefix(s);
        static bool IsNotPresent(string[] lines, string s) => Array.BinarySearch(lines, s, StringComparer.Ordinal) < 0;
    }

    private IEnumerable<(string UnshippedPath, string ShippedPath)> GetAllPublicApiFilePairs()
    {
        return FileSystemHelper
            .EnumerateFiles(_home.HomeDirectory, "**/PublicAPI.Shipped.txt", caseSensitive: true)
            .Select(GetPair)
            .WhereNotNull();

        static (string UnshippedPath, string ShippedPath)? GetPair(string shippedPath)
        {
            var unshippedPath = Path.Combine(Path.GetDirectoryName(shippedPath)!, "PublicAPI.Unshipped.txt");
            return FileSystemHelper.FileExists(unshippedPath) ? (unshippedPath, shippedPath) : null;
        }
    }
}
