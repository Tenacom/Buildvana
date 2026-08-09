// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Buildvana.Core;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services;

/// <summary>
/// Inspects and rewrites the contents of a changelog in Markdown format, according to the
/// <see href="https://keepachangelog.com/en/1.1.0/">Keep a Changelog</see> specification.
/// </summary>
/// <remarks>
/// <para>The methods of this class work on changelog contents already read, leaving file access
/// to <see cref="ChangelogService"/>.</para>
/// </remarks>
internal static partial class ChangelogUpdater
{
    /// <summary>
    /// Checks a changelog for contents in the "Unreleased changes" section.
    /// </summary>
    /// <param name="lines">The lines of the changelog.</param>
    /// <returns>If there are any contents (excluding blank lines and subsection headings)
    /// in the "Unreleased changes" section, <see langword="true"/>; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="BuildFailedException">The changelog contains no sections.</exception>
    public static bool HasUnreleasedChanges(IReadOnlyList<string> lines)
    {
        Guard.IsNotNull(lines);
        var sectionHeadingRegex = GetSectionHeadingRegex();
        var subSectionHeadingRegex = GetSubsectionHeadingRegex();
        var lineIndex = 0;
        string? line;
        do
        {
            line = GetLineOrNull(lines, lineIndex++);
        } while (line is not null && !sectionHeadingRegex.IsMatch(line));

        BuildFailedException.ThrowIfNot(line is not null, $"{ChangelogService.FileName} contains no sections.");
        for (; ;)
        {
            line = GetLineOrNull(lines, lineIndex++);
            if (line is null || sectionHeadingRegex.IsMatch(line))
            {
                break;
            }

            if (!string.IsNullOrWhiteSpace(line) && !subSectionHeadingRegex.IsMatch(line))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Rewrites a changelog for a new release, moving the contents of the "Unreleased changes" section
    /// to a new section.
    /// </summary>
    /// <param name="lines">The lines of the changelog.</param>
    /// <param name="makeSectionTitle">A callback that produces the title of the new section. It is called
    /// at most once, and only if a new section is actually written.</param>
    /// <param name="emptyChangelogSubstitute">Text to use as the new section's body when the "Unreleased changes"
    /// section has no content. When <see langword="null"/>, or all whitespace, an empty section is moved
    /// verbatim (producing a title-only section).</param>
    /// <returns>The rewritten changelog, with lines separated by <c>"\n"</c>.</returns>
    /// <exception cref="BuildFailedException">The changelog contains no sections.</exception>
    public static string PrepareForRelease(IReadOnlyList<string> lines, Func<string> makeSectionTitle, string? emptyChangelogSubstitute)
    {
        Guard.IsNotNull(lines);
        Guard.IsNotNull(makeSectionTitle);

        // Using a StringWriter instead of a StringBuilder allows for a custom line separator
        // Under Windows, a StringBuilder would only use "\r\n" as a line separator, which would be wrong in this case
        var sb = new StringBuilder();
        using var writer = new StringWriter(sb, CultureInfo.InvariantCulture);
        writer.NewLine = "\n";
        var sectionHeadingRegex = GetSectionHeadingRegex();
        var subSectionHeadingRegex = GetSubsectionHeadingRegex();
        var subSections = new List<(string Header, List<string> Lines)> { (string.Empty, []) };
        var subSectionIndex = 0;

        const int readingFileHeader = 0;
        const int readingUnreleasedChangesSection = 1;
        const int readingRemainderOfFile = 2;
        const int readingDone = 3;
        var lineIndex = 0;
        var state = readingFileHeader;
        while (state != readingDone)
        {
            var line = GetLineOrNull(lines, lineIndex++);
            switch (state)
            {
                case readingFileHeader:
                    BuildFailedException.ThrowIfNot(line is not null, $"{ChangelogService.FileName} contains no sections.");

                    // Copy everything up to an including the first section heading (which we assume is "Unreleased changes")
                    writer.WriteLine(line);
                    if (sectionHeadingRegex.IsMatch(line))
                    {
                        state = readingUnreleasedChangesSection;
                    }

                    break;
                case readingUnreleasedChangesSection:
                    if (line is null)
                    {
                        // The changelog only contains the "Unreleased changes" section;
                        // this happens when no release has been published yet
                        WriteNewSections(true);
                        state = readingDone;
                        break;
                    }

                    if (sectionHeadingRegex.IsMatch(line))
                    {
                        // Reached header of next section
                        WriteNewSections(false);
                        writer.WriteLine(line);
                        state = readingRemainderOfFile;
                        break;
                    }

                    if (subSectionHeadingRegex.IsMatch(line))
                    {
                        subSections.Add((line, []));
                        ++subSectionIndex;
                        break;
                    }

                    subSections[subSectionIndex].Lines.Add(line);
                    break;
                case readingRemainderOfFile:
                    if (line is null)
                    {
                        state = readingDone;
                        break;
                    }

                    writer.WriteLine(line);
                    break;
            }
        }

        return sb.ToString();

        void WriteNewSections(bool atEndOfFile)
        {
            // Create empty subsections in new "Unreleased changes" section
            foreach (var (header, _) in subSections.Skip(1))
            {
                writer.WriteLine(string.Empty);
                writer.WriteLine(header);
            }

            // Write header of new release section
            writer.WriteLine(string.Empty);
            writer.WriteLine("## " + makeSectionTitle());

            var newSectionLines = CollectNewSectionLines();
            var newSectionCount = newSectionLines.Count;
            if (atEndOfFile)
            {
                // If there is no other section after the new release,
                // we don't want extra blank lines at EOF
                while (newSectionCount > 0 && string.IsNullOrEmpty(newSectionLines[newSectionCount - 1]))
                {
                    --newSectionCount;
                }
            }

            foreach (var newSectionLine in newSectionLines.Take(newSectionCount))
            {
                writer.WriteLine(newSectionLine);
            }
        }

        List<string> CollectNewSectionLines()
        {
            var result = new List<string>(subSections[0].Lines);

            // Copy only subsections that have actual content
            foreach (var (header, subSectionLines) in subSections.Skip(1).Where(s => s.Lines.Any(l => !string.IsNullOrWhiteSpace(l))))
            {
                result.Add(header);
                result.AddRange(subSectionLines);
            }

            // When the "Unreleased changes" section has no real content, substitute the configured text (if any).
            // The substitute replaces the whole body of the section, blank lines included, so it is trimmed
            // and surrounded with blank lines of our own: whatever padding it was configured with, the new
            // section reads like every other one. A substitute that is all whitespace has nothing to
            // contribute once trimmed, and is treated as no substitute at all.
            var substitute = emptyChangelogSubstitute is not null && result.All(string.IsNullOrWhiteSpace)
                ? emptyChangelogSubstitute.ReplaceLineEndings("\n").Trim()
                : null;
            if (!string.IsNullOrEmpty(substitute))
            {
                result.Clear();
                result.Add(string.Empty);
                result.AddRange(substitute.Split('\n'));
                result.Add(string.Empty);
            }

            return result;
        }
    }

    /// <summary>
    /// Rewrites a changelog, replacing the heading of the first section after the "Unreleased changes" section
    /// to reflect a change in the released version.
    /// </summary>
    /// <param name="lines">The lines of the changelog.</param>
    /// <param name="makeSectionTitle">A callback that produces the new title of the section. It is called
    /// at most once, and only if a section heading is actually replaced.</param>
    /// <returns>The rewritten changelog, with lines separated by <c>"\n"</c>.</returns>
    /// <exception cref="BuildFailedException">The changelog contains no sections, or only one section.</exception>
    public static string UpdateNewSectionTitle(IReadOnlyList<string> lines, Func<string> makeSectionTitle)
    {
        Guard.IsNotNull(lines);
        Guard.IsNotNull(makeSectionTitle);

        // Using a StringWriter instead of a StringBuilder allows for a custom line separator
        // Under Windows, a StringBuilder would only use "\r\n" as a line separator, which would be wrong in this case
        var sb = new StringBuilder();
        using var writer = new StringWriter(sb, CultureInfo.InvariantCulture);
        writer.NewLine = "\n";
        var sectionHeadingRegex = GetSectionHeadingRegex();

        const int readingFileHeader = 0;
        const int readingUnreleasedChangesSection = 1;
        const int readingRemainderOfFile = 2;
        const int readingDone = 3;
        var lineIndex = 0;
        var state = readingFileHeader;
        while (state != readingDone)
        {
            var line = GetLineOrNull(lines, lineIndex++);
            switch (state)
            {
                case readingFileHeader:
                    BuildFailedException.ThrowIfNot(line is not null, $"{ChangelogService.FileName} contains no sections.");
                    writer.WriteLine(line);
                    if (sectionHeadingRegex.IsMatch(line))
                    {
                        state = readingUnreleasedChangesSection;
                    }

                    break;
                case readingUnreleasedChangesSection:
                    BuildFailedException.ThrowIfNot(line is not null, $"{ChangelogService.FileName} contains only one section.");
                    if (sectionHeadingRegex.IsMatch(line))
                    {
                        // Replace header of second section
                        writer.WriteLine("## " + makeSectionTitle());
                        state = readingRemainderOfFile;
                        break;
                    }

                    writer.WriteLine(line);
                    break;
                case readingRemainderOfFile:
                    if (line is null)
                    {
                        state = readingDone;
                        break;
                    }

                    writer.WriteLine(line);
                    break;
            }
        }

        return sb.ToString();
    }

    // Reading past the last line yields null, just as TextReader.ReadLine does at end of file:
    // the state machines above pivot on it to recognize the end of the changelog.
    private static string? GetLineOrNull(IReadOnlyList<string> lines, int index) => index < lines.Count ? lines[index] : null;

    [GeneratedRegex("^ {0,3}##($|[^#])", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex GetSectionHeadingRegex();

    [GeneratedRegex("^ {0,3}###($|[^#])", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex GetSubsectionHeadingRegex();
}
