// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.Testing;

/// <summary>
/// A page of the user documentation, read by the tests that pin a reference page against the code it lists.
/// </summary>
/// <remarks>
/// <para>The reader knows the shape the documentation rules give a page: ATX headings, pipe tables with a header
/// row and a delimiter row, and fenced code blocks, from which no heading and no table is read. A cell comes back
/// with the backticks of its code spans stripped and its whitespace trimmed, so that a code listed as
/// <c>`BV1100`</c> and one listed as <c>BV1100</c> compare equal. A cell holding a pipe inside a code span is
/// split at the pipe, because no reference page has one.</para>
/// </remarks>
public sealed partial class DocumentationPage
{
    private readonly List<(int Index, int Level, string Text)> _headings = [];
    private readonly string[] _lines;
    private readonly bool[] _inFence;

    private DocumentationPage(string filePath, string[] lines)
    {
        FilePath = filePath;
        _lines = lines;
        _inFence = new bool[lines.Length];
        var open = false;
        for (var i = 0; i < lines.Length; i++)
        {
            if (FenceRegex().IsMatch(lines[i]))
            {
                open = !open;
                _inFence[i] = true;
                continue;
            }

            _inFence[i] = open;
            if (!open && HeadingRegex().Match(lines[i]) is { Success: true } match)
            {
                _headings.Add((i, match.Groups["hashes"].Value.Length, match.Groups["text"].Value));
            }
        }
    }

    /// <summary>
    /// Gets the full path of the page.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Loads a page.
    /// </summary>
    /// <param name="filePath">The full path of the page.</param>
    /// <returns>The loaded page.</returns>
    public static DocumentationPage Load(string filePath)
    {
        Guard.IsNotNullOrEmpty(filePath);
        return new(filePath, File.ReadAllLines(filePath));
    }

    /// <summary>
    /// Gets the text of every heading of a level, in document order.
    /// </summary>
    /// <param name="level">The heading level, from 1 to 6.</param>
    /// <returns>The texts of the headings.</returns>
    public IReadOnlyList<string> GetHeadings(int level)
        => [.. _headings.Where(heading => heading.Level == level).Select(heading => heading.Text)];

    /// <summary>
    /// Gets the cells of one column of the first table under a heading.
    /// </summary>
    /// <param name="heading">The text of the heading, at any level.</param>
    /// <param name="column">The text of the header cell of the column.</param>
    /// <returns>The cells of the column, in row order, with the backticks of code spans stripped and whitespace
    /// trimmed. An empty list when the section holds no table.</returns>
    /// <exception cref="InvalidOperationException">The page has no heading with the given text, or the table has
    /// no column with the given header.</exception>
    public IReadOnlyList<string> GetTableColumn(string heading, string column)
    {
        Guard.IsNotNull(heading);
        Guard.IsNotNull(column);
        var index = _headings.FindIndex(candidate => string.Equals(candidate.Text, heading, StringComparison.Ordinal));
        if (index < 0)
        {
            throw new InvalidOperationException($"{FilePath} has no heading \"{heading}\".");
        }

        var (start, level, _) = _headings[index];
        var nextHeadings = _headings.Skip(index + 1).Where(candidate => candidate.Level <= level);
        var end = nextHeadings.Select(candidate => candidate.Index).DefaultIfEmpty(_lines.Length).First();
        var rows = new List<string[]>();
        for (var i = start + 1; i < end; i++)
        {
            var isRow = !_inFence[i] && _lines[i].TrimStart().StartsWith('|');
            if (isRow)
            {
                rows.Add(SplitRow(_lines[i]));
            }
            else if (rows.Count > 0)
            {
                break;
            }
        }

        if (rows.Count == 0)
        {
            return [];
        }

        var columnIndex = Array.FindIndex(rows[0], cell => string.Equals(cell, column, StringComparison.Ordinal));
        if (columnIndex < 0)
        {
            throw new InvalidOperationException($"The table under \"{heading}\" of {FilePath} has no \"{column}\" column.");
        }

        // The first row is the header, and the second is the delimiter row.
        return [.. rows.Skip(2).Select(row => columnIndex < row.Length ? row[columnIndex] : string.Empty)];
    }

    // The cells of a row, without the outer pipes, the backticks of code spans, and the surrounding whitespace.
    private static string[] SplitRow(string line)
    {
        var inner = line.Trim()[1..];
        if (inner.EndsWith('|'))
        {
            inner = inner[..^1];
        }

        return [.. inner.Split('|').Select(cell => cell.Replace("`", string.Empty, StringComparison.Ordinal).Trim())];
    }

    [GeneratedRegex(@"^(?<hashes>#{1,6}) (?<text>.+?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^\s*(?:`{3,}|~{3,})", RegexOptions.CultureInvariant)]
    private static partial Regex FenceRegex();
}
