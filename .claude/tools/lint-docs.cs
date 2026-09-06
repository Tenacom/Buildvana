// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

/*
 * Checks the user documentation against the rules of `.claude/rules/documentation.md` that a tool can measure.
 *
 * Run from the repo root:
 *   `dotnet run .claude/tools/lint-docs.cs [<root>]`
 * <root> is the repository root, and defaults to the current directory. A finding names its file relative to
 * the root when the root was not given, and by its full path otherwise, so that inspect.cs, which passes the
 * root, gets the full paths its problem matcher expects.
 *
 * The documentation set is README.md, CHANGELOG.md, every .md file under docs/, and the NuGet-README.md of each
 * project directory under src/. The tool writes one `path(line,col): error Code: message` per finding to
 * standard output, in the shape the `inspect` task of .vscode/tasks.json parses. It exits 1 when there is any
 * finding, 0 when there is none, and 2 on a usage error. The codes are:
 *   DocsFileName   a file or folder under docs/ whose name is not kebab-case, or a folder two levels deep;
 *   DocsTemplate   a page that departs from the page template: the title, the summary paragraph, the table of
 *                  contents block, a ruler before each level 2 heading, no heading below level 4, and no
 *                  "Overview" section;
 *   DocsToc        a table of contents that does not list the headings of level 2 and below, in text and order;
 *   DocsIndex      a page that docs/README.md does not link, or a table of contents in docs/README.md;
 *   DocsLink       an inline link whose relative target names no file, or no heading of that file;
 *   DocsFence      a fenced block with no language tag, or with a tag outside the list of the rules;
 *   DocsTodo       the word TODO;
 *   DocsRegion     a generated-region marker without its pair;
 *   DocsChangelog  a relative file link in a released section of CHANGELOG.md.
 *
 * Only inline links, `[text](target)`, are checked. A reference-style link is not. A link inside a code span or
 * a fenced block is not a link. A relative target must match the case of the file name, so that a link that
 * resolves on Windows and not on Linux is a finding on both.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

const string Usage = "Usage: dotnet run .claude/tools/lint-docs.cs [<root>]";
const string IndexPath = "docs/README.md";
const string ChangelogPath = "CHANGELOG.md";
const string DisableMarker = "<!-- markdownlint-disable MD036 -->";
const string TocTitle = "**Table of contents**";
const string EnableMarker = "<!-- markdownlint-enable MD036 -->";
const string Ruler = "---";
const string UnreleasedHeading = "Unreleased changes";

// The language tags `.claude/rules/documentation.md` admits on a fenced block.
string[] fenceTags = ["csharp", "json", "jsonc", "markdown", "powershell", "shell", "text", "xml", "yaml"];

// Files the TODO check leaves alone. None here; a copy of this tool may name some.
string[] todoExemptFiles = [];

if (args.Length > 1)
{
    Console.Error.WriteLine(Usage);
    return 2;
}

var rootGiven = args.Length == 1;
var root = Path.GetFullPath(rootGiven ? args[0] : Environment.CurrentDirectory);
var docsDirectory = Path.Combine(root, "docs");
if (!Directory.Exists(docsDirectory) || !File.Exists(Path.Combine(root, ChangelogPath)))
{
    Console.Error.WriteLine($"{root} holds no docs/ directory, or no CHANGELOG.md.");
    Console.Error.WriteLine(Usage);
    return 2;
}

var kebabRegex = new Regex(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant);
var fenceOpenRegex = new Regex(@"^\s*(?:`{3,}|~{3,})\s*(?<tag>\S*)", RegexOptions.CultureInvariant);
var fenceCloseRegex = new Regex(@"^\s*(?:`{3,}|~{3,})\s*$", RegexOptions.CultureInvariant);
var headingRegex = new Regex(@"^(?<hashes>#{1,6}) (?<text>.+?)\s*$", RegexOptions.CultureInvariant);
var codeSpanRegex = new Regex(@"`[^`]*`", RegexOptions.CultureInvariant);
var linkRegex = new Regex(@"!?\[[^\]]*\]\((?<target>[^)\s]+)(?:\s+""[^""]*"")?\)", RegexOptions.CultureInvariant);
var schemeRegex = new Regex(@"^[a-z][a-z0-9+.-]*:", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
var regionRegex = new Regex(@"<!-- (?<name>[A-Za-z0-9_-]+):(?<kind>START|END)\b[^>]*-->", RegexOptions.CultureInvariant);
var tocEntryRegex = new Regex(@"^(?<indent> *)- \[(?<text>.+)\]\(#(?<anchor>[^)]+)\)$", RegexOptions.CultureInvariant);
var todoRegex = new Regex(@"\bTODO\b", RegexOptions.CultureInvariant);

var findings = new List<(string File, int Line, int Column, string Code, string Message)>();

// The documentation set, as relative paths with forward slashes.
var pages = Directory.EnumerateFiles(docsDirectory, "*.md", SearchOption.AllDirectories)
    .Select(path => ToRelative(root, path))
    .Order(StringComparer.Ordinal)
    .ToList();
var files = new List<string> { "README.md", ChangelogPath };
files.AddRange(pages);
var srcDirectory = Path.Combine(root, "src");
if (Directory.Exists(srcDirectory))
{
    files.AddRange(Directory.EnumerateDirectories(srcDirectory)
        .Select(directory => Path.Combine(directory, "NuGet-README.md"))
        .Where(File.Exists)
        .Select(path => ToRelative(root, path))
        .Order(StringComparer.Ordinal));
}

files = files.Where(file => File.Exists(Path.Combine(root, file))).ToList();

// File and folder names under docs/.
foreach (var entry in Directory.EnumerateFileSystemEntries(docsDirectory, "*", SearchOption.AllDirectories))
{
    var relative = ToRelative(root, entry);
    var segments = relative.Split('/');
    var name = segments[^1];
    if (Directory.Exists(entry))
    {
        if (segments.Length > 2)
        {
            Report(relative, 1, 1, "DocsFileName", $"folder \"{relative}\" is more than one level deep under docs/");
        }
        else if (!kebabRegex.IsMatch(name))
        {
            Report(relative, 1, 1, "DocsFileName", $"folder name \"{name}\" is not kebab-case");
        }

        continue;
    }

    var isPage = name.EndsWith(".md", StringComparison.Ordinal) && !string.Equals(relative, IndexPath, StringComparison.Ordinal);
    if (isPage && !kebabRegex.IsMatch(Path.GetFileNameWithoutExtension(name)))
    {
        Report(relative, 1, 1, "DocsFileName", $"file name \"{name}\" is not kebab-case");
    }
}

// Every file is parsed once. A file linked from the set but outside it is parsed on demand, for its headings.
var linesByFile = new Dictionary<string, string[]>(StringComparer.Ordinal);
var inFenceByFile = new Dictionary<string, bool[]>(StringComparer.Ordinal);
var headingsByFile = new Dictionary<string, List<(int Line, int Level, string Text, string Slug)>>(StringComparer.Ordinal);
var linkTargetsByFile = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
foreach (var file in files)
{
    _ = Load(file);
}

foreach (var file in files)
{
    var lines = linesByFile[file];
    var inFence = inFenceByFile[file];
    var isPage = file.StartsWith("docs/", StringComparison.Ordinal) && !string.Equals(file, IndexPath, StringComparison.Ordinal);
    var isChangelog = string.Equals(file, ChangelogPath, StringComparison.Ordinal);
    var checkTodo = !todoExemptFiles.Contains(file, StringComparer.Ordinal);
    var openRegions = new Dictionary<string, int>(StringComparer.Ordinal);
    var releasedSection = string.Empty;
    var targets = new HashSet<string>(StringComparer.Ordinal);
    linkTargetsByFile[file] = targets;

    for (var i = 0; i < lines.Length; i++)
    {
        var line = lines[i];
        var lineNumber = i + 1;
        if (checkTodo)
        {
            foreach (Match match in todoRegex.Matches(line))
            {
                Report(file, lineNumber, match.Index + 1, "DocsTodo", "the file contains \"TODO\"");
            }
        }

        if (inFence[i])
        {
            // The opening fence carries the tag. Every other line of the block is code.
            var isOpener = i == 0 || !inFence[i - 1];
            if (isOpener)
            {
                CheckFenceTag(file, lineNumber, line);
            }

            continue;
        }

        foreach (Match match in regionRegex.Matches(line))
        {
            var regionName = match.Groups["name"].Value;
            var isStart = string.Equals(match.Groups["kind"].Value, "START", StringComparison.Ordinal);
            if (isStart && !openRegions.TryAdd(regionName, lineNumber))
            {
                Report(file, lineNumber, match.Index + 1, "DocsRegion", $"region \"{regionName}\" starts twice");
            }
            else if (!isStart && !openRegions.Remove(regionName))
            {
                Report(file, lineNumber, match.Index + 1, "DocsRegion", $"region \"{regionName}\" ends without starting");
            }
        }

        if (isChangelog)
        {
            var heading = headingRegex.Match(line);
            if (heading.Success && heading.Groups["hashes"].Value.Length == 2)
            {
                // A released section is headed by a link to the release, and the version is its text.
                var text = heading.Groups["text"].Value;
                var versionLink = Regex.Match(text, @"^\[(?<version>[^\]]+)\]", RegexOptions.CultureInvariant);
                var shownSection = versionLink.Success ? versionLink.Groups["version"].Value : text;
                releasedSection = string.Equals(text, UnreleasedHeading, StringComparison.Ordinal) ? string.Empty : shownSection;
            }
        }

        var prose = codeSpanRegex.Replace(line, match => new string(' ', match.Length));
        foreach (Match match in linkRegex.Matches(prose))
        {
            var target = match.Groups["target"].Value;
            var column = match.Index + 1;
            if (releasedSection.Length > 0 && !target.StartsWith('#') && !schemeRegex.IsMatch(target))
            {
                var message = $"released section \"{releasedSection}\" links \"{target}\" by relative path";
                Report(file, lineNumber, column, "DocsChangelog", message);
            }

            CheckLink(file, lineNumber, column, target, targets);
        }
    }

    foreach (var (regionName, startLine) in openRegions)
    {
        Report(file, startLine, 1, "DocsRegion", $"region \"{regionName}\" does not end");
    }

    if (isPage)
    {
        CheckTemplate(file);
    }
}

// The index links every page, and has no table of contents because it is one.
if (linesByFile.TryGetValue(IndexPath, out var indexLines))
{
    var tocLine = Array.IndexOf(indexLines, TocTitle);
    if (tocLine >= 0)
    {
        Report(IndexPath, tocLine + 1, 1, "DocsIndex", "the index has a table of contents, and it is one");
    }

    foreach (var page in pages.Where(page => !string.Equals(page, IndexPath, StringComparison.Ordinal)))
    {
        if (!linkTargetsByFile[IndexPath].Contains(page))
        {
            Report(IndexPath, 1, 1, "DocsIndex", $"the index does not link \"{page}\"");
        }
    }
}

var orderedFindings = findings.OrderBy(f => f.File, StringComparer.Ordinal).ThenBy(f => f.Line).ThenBy(f => f.Column);
foreach (var (file, line, column, code, message) in orderedFindings)
{
    var shownPath = rootGiven ? Path.Combine(root, file.Replace('/', Path.DirectorySeparatorChar)) : file;
    Console.WriteLine($"{shownPath}({line},{column}): error {code}: {message}");
}

Console.Error.WriteLine($"=== lint-docs: {findings.Count} finding(s) ===");
return findings.Count > 0 ? 1 : 0;

void Report(string file, int line, int column, string code, string message)
{
    findings.Add((file, line, column, code, message));
}

// Reads a file, marks the lines inside fenced blocks, and lists its headings with their GitHub anchors.
// Returns false when the file does not exist.
bool Load(string file)
{
    if (linesByFile.ContainsKey(file))
    {
        return true;
    }

    var fullPath = Path.Combine(root, file.Replace('/', Path.DirectorySeparatorChar));
    if (!File.Exists(fullPath))
    {
        return false;
    }

    var lines = File.ReadAllLines(fullPath);
    var inFence = new bool[lines.Length];
    var headings = new List<(int Line, int Level, string Text, string Slug)>();
    var slugCounts = new Dictionary<string, int>(StringComparer.Ordinal);
    var open = false;
    for (var i = 0; i < lines.Length; i++)
    {
        if (open)
        {
            inFence[i] = true;
            if (fenceCloseRegex.IsMatch(lines[i]))
            {
                open = false;
            }

            continue;
        }

        if (fenceOpenRegex.IsMatch(lines[i]))
        {
            inFence[i] = true;
            open = true;
            continue;
        }

        var match = headingRegex.Match(lines[i]);
        if (!match.Success)
        {
            continue;
        }

        var text = match.Groups["text"].Value;
        var slug = Slug(text);
        if (slugCounts.TryGetValue(slug, out var count))
        {
            slugCounts[slug] = count + 1;
            slug = $"{slug}-{count}";
        }
        else
        {
            slugCounts[slug] = 1;
        }

        headings.Add((i + 1, match.Groups["hashes"].Value.Length, text, slug));
    }

    linesByFile[file] = lines;
    inFenceByFile[file] = inFence;
    headingsByFile[file] = headings;
    return true;
}

void CheckFenceTag(string file, int lineNumber, string line)
{
    var tag = fenceOpenRegex.Match(line).Groups["tag"].Value;
    if (tag.Length == 0)
    {
        Report(file, lineNumber, 1, "DocsFence", "the fenced block has no language tag");
    }
    else if (!fenceTags.Contains(tag, StringComparer.Ordinal))
    {
        Report(file, lineNumber, 1, "DocsFence", $"the language tag \"{tag}\" is not one of {string.Join(", ", fenceTags)}");
    }
}

void CheckLink(string file, int lineNumber, int column, string target, HashSet<string> targets)
{
    if (target.StartsWith('#'))
    {
        CheckAnchor(file, lineNumber, column, file, target[1..]);
        return;
    }

    if (schemeRegex.IsMatch(target))
    {
        return;
    }

    if (target.Contains('\\', StringComparison.Ordinal))
    {
        Report(file, lineNumber, column, "DocsLink", $"the link target \"{target}\" holds a backslash");
        return;
    }

    var hashIndex = target.IndexOf('#', StringComparison.Ordinal);
    var pathPart = hashIndex < 0 ? target : target[..hashIndex];
    var anchor = hashIndex < 0 ? null : target[(hashIndex + 1)..];
    var fileDirectory = Path.GetDirectoryName(Path.Combine(root, file.Replace('/', Path.DirectorySeparatorChar))) ?? root;
    var fullTarget = Path.GetFullPath(Path.Combine(fileDirectory, pathPart));
    var relativeTarget = ToRelative(root, fullTarget);
    if (relativeTarget.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativeTarget))
    {
        Report(file, lineNumber, column, "DocsLink", $"the link target \"{target}\" points outside the repository");
        return;
    }

    if (!ExistsWithExactCase(relativeTarget))
    {
        Report(file, lineNumber, column, "DocsLink", $"the link target \"{target}\" names no file");
        return;
    }

    _ = targets.Add(relativeTarget);
    var isMarkdown = relativeTarget.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
    if (anchor is not null && isMarkdown)
    {
        CheckAnchor(file, lineNumber, column, relativeTarget, anchor);
    }
}

void CheckAnchor(string file, int lineNumber, int column, string targetFile, string anchor)
{
    if (!Load(targetFile))
    {
        return;
    }

    var exists = headingsByFile[targetFile].Any(heading => string.Equals(heading.Slug, anchor, StringComparison.Ordinal));
    if (!exists)
    {
        Report(file, lineNumber, column, "DocsLink", $"the anchor \"#{anchor}\" names no heading of {targetFile}");
    }
}

// Walks the path one segment at a time, comparing names with the directory listing, so that a target whose
// case differs from the file's is a finding on a case-insensitive file system too.
bool ExistsWithExactCase(string relativePath)
{
    var current = root;
    foreach (var segment in relativePath.Split('/'))
    {
        if (!Directory.Exists(current))
        {
            return false;
        }

        var names = Directory.EnumerateFileSystemEntries(current).Select(Path.GetFileName);
        if (!names.Contains(segment, StringComparer.Ordinal))
        {
            return false;
        }

        current = Path.Combine(current, segment);
    }

    return true;
}

void CheckTemplate(string file)
{
    var lines = linesByFile[file];
    var headings = headingsByFile[file];
    foreach (var (line, level, text, _) in headings)
    {
        var hashes = new string('#', level);
        if (level > 4)
        {
            Report(file, line, 1, "DocsTemplate", $"the heading \"{hashes} {text}\" is below level 4");
        }

        if (level == 2 && string.Equals(text, "Overview", StringComparison.Ordinal))
        {
            const string Message = "an \"Overview\" section: put its content in the summary paragraph, or under a heading that names it";
            Report(file, line, 1, "DocsTemplate", Message);
        }

        if (level == 2)
        {
            CheckRuler(file, lines, line, text);
        }
    }

    if (lines.Length == 0 || !lines[0].StartsWith("# ", StringComparison.Ordinal))
    {
        Report(file, 1, 1, "DocsTemplate", "the first line is not the title, a level 1 heading");
        return;
    }

    if (!Expect(file, lines, 1, string.Empty))
    {
        return;
    }

    var summary = lines.Length > 2 ? lines[2] : string.Empty;
    var isProse = summary.Length > 0
        && !summary.StartsWith('#')
        && !summary.StartsWith('<')
        && !string.Equals(summary, Ruler, StringComparison.Ordinal);
    if (!isProse)
    {
        Report(file, 3, 1, "DocsTemplate", "no summary paragraph between the title and the table of contents");
        return;
    }

    var summaryEnd = 2;
    while (summaryEnd < lines.Length && lines[summaryEnd].Length > 0)
    {
        summaryEnd++;
    }

    string[] tocHeader = [Ruler, string.Empty, DisableMarker, TocTitle, EnableMarker, string.Empty];
    for (var k = 0; k < tocHeader.Length; k++)
    {
        if (!Expect(file, lines, summaryEnd + 1 + k, tocHeader[k]))
        {
            return;
        }
    }

    var listStart = summaryEnd + 1 + tocHeader.Length;
    var listEnd = listStart;
    while (listEnd < lines.Length && lines[listEnd].Length > 0)
    {
        listEnd++;
    }

    var isClosed = Expect(file, lines, listEnd + 1, Ruler) && Expect(file, lines, listEnd + 2, string.Empty);
    if (!isClosed)
    {
        return;
    }

    var firstSectionIndex = listEnd + 3;
    if (firstSectionIndex >= lines.Length || !lines[firstSectionIndex].StartsWith("## ", StringComparison.Ordinal))
    {
        Report(file, firstSectionIndex + 1, 1, "DocsTemplate", "the first level 2 heading does not follow the table of contents");
        return;
    }

    CheckToc(file, lines, listStart, listEnd, headings);
}

bool Expect(string file, string[] lines, int index, string expected)
{
    var found = index < lines.Length && string.Equals(lines[index], expected, StringComparison.Ordinal);
    if (!found)
    {
        var shown = expected.Length == 0 ? "a blank line" : $"\"{expected}\"";
        Report(file, index + 1, 1, "DocsTemplate", $"expected {shown} here, per the page template");
    }

    return found;
}

void CheckRuler(string file, string[] lines, int headingLine, string text)
{
    var index = headingLine - 1;
    var hasBlank = index >= 1 && lines[index - 1].Length == 0;
    var hasRuler = index >= 2 && string.Equals(lines[index - 2], Ruler, StringComparison.Ordinal);
    if (!hasBlank || !hasRuler)
    {
        Report(file, headingLine, 1, "DocsTemplate", $"no ruler before \"## {text}\"");
        return;
    }

    var hasBlankAboveRuler = index >= 3 && lines[index - 3].Length == 0;
    if (!hasBlankAboveRuler)
    {
        Report(file, headingLine - 2, 1, "DocsTemplate", $"no blank line before the ruler of \"## {text}\"");
    }
}

void CheckToc(
    string file,
    string[] lines,
    int listStart,
    int listEnd,
    List<(int Line, int Level, string Text, string Slug)> headings)
{
    var expected = headings.Where(heading => heading.Level >= 2).ToList();
    var count = Math.Max(listEnd - listStart, expected.Count);
    for (var k = 0; k < count; k++)
    {
        var entryIndex = listStart + k;
        if (entryIndex >= listEnd)
        {
            var (_, level, text, _) = expected[k];
            Report(file, listEnd, 1, "DocsToc", $"the table of contents does not list \"{new string('#', level)} {text}\"");
            return;
        }

        var match = tocEntryRegex.Match(lines[entryIndex]);
        if (!match.Success)
        {
            Report(file, entryIndex + 1, 1, "DocsToc", $"\"{lines[entryIndex]}\" is not a table of contents entry");
            return;
        }

        if (k >= expected.Count)
        {
            Report(file, entryIndex + 1, 1, "DocsToc", $"\"{lines[entryIndex]}\" lists no heading");
            return;
        }

        var (_, expectedLevel, expectedText, expectedSlug) = expected[k];
        var expectedEntry = $"{new string(' ', 2 * (expectedLevel - 2))}- [{expectedText}](#{expectedSlug})";
        if (!string.Equals(lines[entryIndex], expectedEntry, StringComparison.Ordinal))
        {
            Report(file, entryIndex + 1, 1, "DocsToc", $"expected \"{expectedEntry}\", found \"{lines[entryIndex]}\"");
            return;
        }
    }
}

static string ToRelative(string root, string path)
{
    return Path.GetRelativePath(root, path).Replace('\\', '/');
}

// GitHub's anchor for a heading: the text in lowercase, with letters, digits, hyphens and underscores kept, a
// space turned into a hyphen, and everything else dropped. Markdown formatting is punctuation to it, so the
// backticks of a code span go, and the text of a link stays while its target goes.
static string Slug(string headingText)
{
    var text = Regex.Replace(headingText, @"\[([^\]]*)\]\([^)]*\)", "$1", RegexOptions.CultureInvariant);
    var builder = new StringBuilder();
    foreach (var ch in text)
    {
        var lower = char.ToLowerInvariant(ch);
        if (char.IsLetterOrDigit(lower) || lower == '_' || lower == '-')
        {
            _ = builder.Append(lower);
        }
        else if (lower == ' ')
        {
            _ = builder.Append('-');
        }
    }

    return builder.ToString();
}
