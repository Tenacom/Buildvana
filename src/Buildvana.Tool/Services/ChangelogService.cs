// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Buildvana.Tool.Services.ServerAdapters;
using Buildvana.Tool.Services.Versioning;
using Buildvana.Tool.Utilities;
using Cake.Common.Diagnostics;
using Cake.Core;
using Cake.Core.IO;
using CommunityToolkit.Diagnostics;

using SysFile = System.IO.File;

namespace Buildvana.Tool.Services;

/// <summary>
/// Manages the repository's changelog in Markdown format, according to the <see href="https://keepachangelog.com/en/1.1.0/">Keep a Changelog</see> specification.
/// </summary>
public sealed partial class ChangelogService
{
    /// <summary>
    /// The name of the changelog file.
    /// </summary>
    public const string FileName = "CHANGELOG.md";

    private readonly ICakeContext _context;
    private readonly ServerAdapter _server;
    private readonly VersionService _version;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangelogService"/> class.
    /// </summary>
    public ChangelogService(ICakeContext context, ServerAdapter server, VersionService version)
    {
        Guard.IsNotNull(context);
        Guard.IsNotNull(server);
        Guard.IsNotNull(version);
        _context = context;
        _server = server;
        _version = version;
        Path = new FilePath(FileName);
        FullPath = Path.FullPath;
        Exists = SysFile.Exists(FullPath);
    }

    /// <summary>
    /// Gets the path to the changelog file.
    /// </summary>
    public FilePath Path { get; }

    /// <summary>
    /// Gets the full path to the changelog file as a string.
    /// </summary>
    public string FullPath { get; }

    /// <summary>
    /// Gets a value indicating whether the changelog file exists.
    /// </summary>
    public bool Exists { get; }

    /// <summary>
    /// Checks the changelog for contents in the "Unreleased changes" section.
    /// </summary>
    /// <returns>If there are any contents (excluding blank lines and subsection headings)
    /// in the "Unreleased changes" section, <see langword="true"/>; otherwise, <see langword="false"/>.</returns>
    public bool HasUnreleasedChanges()
    {
        if (!Exists)
        {
            return false;
        }

        using var reader = new StreamReader(FullPath, Encoding.UTF8);
        var sectionHeadingRegex = GetSectionHeadingRegex();
        var subSectionHeadingRegex = GetSubsectionHeadingRegex();
        string? line;
        do
        {
            line = reader.ReadLine();
        } while (line != null && !sectionHeadingRegex.IsMatch(line));

        _context.Ensure(line != null, $"{FileName} contains no sections.");
        for (; ;)
        {
            line = reader.ReadLine();
            if (line == null || sectionHeadingRegex.IsMatch(line))
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
    /// Prepares the changelog for a new release by moving the contents of the "Unreleased changes" section
    /// to a new section.
    /// </summary>
    public void PrepareForRelease()
    {
        _context.Information("Updating changelog...");
        var encoding = new UTF8Encoding(false, true);
        var sb = new StringBuilder();
        using (var reader = new StreamReader(FullPath, encoding))
        using (var writer = new StringWriter(sb, CultureInfo.InvariantCulture))
        {
            // Using a StringWriter instead of a StringBuilder allows for a custom line separator
            // Under Windows, a StringBuilder would only use "\r\n" as a line separator, which would be wrong in this case
            writer.NewLine = "\n";
            var sectionHeadingRegex = GetSectionHeadingRegex();
            var subSectionHeadingRegex = GetSubsectionHeadingRegex();
            var subSections = new List<(string Header, List<string> Lines)> { (string.Empty, []) };
            var subSectionIndex = 0;

            const int readingFileHeader = 0;
            const int readingUnreleasedChangesSection = 1;
            const int readingRemainderOfFile = 2;
            const int readingDone = 3;
            var state = readingFileHeader;
            while (state != readingDone)
            {
                var line = reader.ReadLine();
                switch (state)
                {
                    case readingFileHeader:
                        _context.Ensure(line != null, $"{FileName} contains no sections.");

                        // Copy everything up to an including the first section heading (which we assume is "Unreleased changes")
                        writer.WriteLine(line);
                        if (sectionHeadingRegex.IsMatch(line))
                        {
                            state = readingUnreleasedChangesSection;
                        }

                        break;
                    case readingUnreleasedChangesSection:
                        if (line == null)
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
                            subSections.Add((line, new List<string>()));
                            ++subSectionIndex;
                            break;
                        }

                        subSections[subSectionIndex].Lines.Add(line);
                        break;
                    case readingRemainderOfFile:
                        if (line == null)
                        {
                            state = readingDone;
                            break;
                        }

                        writer.WriteLine(line);
                        break;
                    default:
                        _context.Fail($"Internal error: reading state corrupted ({state}).");
                        throw null;
                }
            }

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
                writer.WriteLine("## " + MakeSectionTitle());

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
                foreach (var (header, lines) in subSections.Skip(1).Where(s => s.Lines.Any(l => !string.IsNullOrWhiteSpace(l))))
                {
                    result.Add(header);
                    result.AddRange(lines);
                }

                return result;
            }
        }

        SysFile.WriteAllText(FullPath, sb.ToString(), encoding);
    }

    /// <summary>
    /// Updates the heading of the first section of the changelog after the "Unreleased changes" section
    /// to reflect a change in the released version.
    /// </summary>
    public void UpdateNewSectionTitle()
    {
        _context.Information("Updating changelog's new release section title...");
        var encoding = new UTF8Encoding(false, true);
        var sb = new StringBuilder();
        using (var reader = new StreamReader(FullPath, encoding))
        using (var writer = new StringWriter(sb, CultureInfo.InvariantCulture))
        {
            // Using a StringWriter instead of a StringBuilder allows for a custom line separator
            // Under Windows, a StringBuilder would only use "\r\n" as a line separator, which would be wrong in this case
            writer.NewLine = "\n";
            var sectionHeadingRegex = GetSectionHeadingRegex();

            const int readingFileHeader = 0;
            const int readingUnreleasedChangesSection = 1;
            const int readingRemainderOfFile = 2;
            const int readingDone = 3;
            var state = readingFileHeader;
            while (state != readingDone)
            {
                var line = reader.ReadLine();
                switch (state)
                {
                    case readingFileHeader:
                        _context.Ensure(line != null, $"{FileName} contains no sections.");
                        writer.WriteLine(line);
                        if (sectionHeadingRegex.IsMatch(line))
                        {
                            state = readingUnreleasedChangesSection;
                        }

                        break;
                    case readingUnreleasedChangesSection:
                        _context.Ensure(line != null, $"{FileName} contains only one section.");
                        if (sectionHeadingRegex.IsMatch(line))
                        {
                            // Replace header of second section
                            writer.WriteLine("## " + MakeSectionTitle());
                            state = readingRemainderOfFile;
                            break;
                        }

                        writer.WriteLine(line);
                        break;
                    case readingRemainderOfFile:
                        if (line == null)
                        {
                            state = readingDone;
                            break;
                        }

                        writer.WriteLine(line);
                        break;
                    default:
                        _context.Fail($"Internal error: reading state corrupted ({state}).");
                        throw null;
                }
            }
        }

        SysFile.WriteAllText(FullPath, sb.ToString(), encoding);
    }

    [GeneratedRegex(@"^ {0,3}##($|[^#])", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex GetSectionHeadingRegex();

    [GeneratedRegex(@"^ {0,3}###($|[^#])", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex GetSubsectionHeadingRegex();

    private string MakeSectionTitle()
        => $"[{_version.CurrentStr}]({_server.GetReleaseUrl(_version.CurrentStr)}) ({DateTime.Now:yyyy-MM-dd})";
}
