// Copyright (C) Tenacom and contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

/*
 * Checks the user documentation against the rules of `.claude/rules/common/documentation.md` that a tool can measure.
 *
 * Run from the repo root:
 *   `dotnet run .claude/tools/lint-docs.cs [<root>]`
 * <root> is the repository root, and defaults to the current directory. A finding names its file relative to
 * the root when the root was not given, and by its full path otherwise, so that inspect.cs, which passes the
 * root, gets the full paths its problem matcher expects.
 *
 * The documentation set is README.md, CHANGELOG.md, every .md file under docs/, and the NuGet-README.md of each
 * project directory under src/. A member that does not exist is skipped, and named on standard error, so that a
 * repository without documentation is a clean run and not an error. The tool writes one
 * `path(line,col): error Code: message` per finding to standard output, in the shape the `inspect` task of
 * .vscode/tasks.json parses. It exits 1 when there is any finding, 0 when there is none, and 2 on a usage error.
 * The codes are:
 *   DocsFileName   a file or folder under docs/ whose name is not kebab-case;
 *   DocsTemplate   a page that departs from the page template: the title, the summary paragraph and the alerts
 *                  after it, the table of contents block, a ruler before each level 2 heading, no heading below
 *                  level 4, and no "Overview" section;
 *   DocsToc        a table of contents that does not list the headings of level 2 and below, in text and order;
 *   DocsIndex      a folder under docs/ that holds a page and no README.md, a page or the index of a subfolder
 *                  that the index of its directory does not link, or a table of contents in an index;
 *   DocsSite       in a repository with a documentation site: a content root other than docs/, no docs/index.md,
 *                  a docs/README.md, a subfolder with pages and no _folder.yml that holds a title, or a folder
 *                  that takes the path `preview`;
 *   DocsLink       an inline link whose relative target names no file, or no heading of that file;
 *   DocsFence      a fenced block with no language tag, or with a tag outside the list of the rules;
 *   DocsTodo       the word TODO;
 *   DocsRegion     a generated-region marker without its pair;
 *   DocsChangelog  a relative file link in a released section of CHANGELOG.md, a link to the documentation site
 *                  or a nested list under "Unreleased changes", or a breaking change after a plain bullet in a
 *                  subsection of "Unreleased changes";
 *   DocsSentence   a prose line, outside a fenced block and a table, holding more than 25 words, or more than
 *                  one sentence; a code span counts as one word, and a link as the words of its text. The
 *                  released sections of CHANGELOG.md and the informal documents are not checked.
 *
 * The facts about the repository come from `ProjectFacts.cs`, which `Common.props` compiles in. They are the
 * globs of the files the TODO check leaves alone, the globs of the informal documents, the URL of the
 * documentation site, and the platform of the Git host. `ProjectFacts.cs` states the glob syntax.
 *
 * An informal document follows `.claude/output-styles/informal-tech.md`, as "Register" in
 * `.claude/rules/common/documentation.md` says. It is a README.md or NuGet-README.md file, a file whose YAML
 * front matter sets `x-informal` to `true`, or a file that matches a glob of `ProjectFacts.cs`. The tool checks
 * no rule of that register. The lines of a front matter are not prose in any file, and hold no heading.
 *
 * Only inline links, `[text](target)`, are checked. A reference-style link is not. A link inside a code span or
 * a fenced block is not a link. A relative target must match the case of the file name, so that a link that
 * resolves on Windows and not on Linux is a finding on both.
 *
 * The published documentation is outside the repository, as "Published documentation" in
 * `.claude/rules/common/documentation.md` says. A link to it has a scheme, so its target is not checked. A bullet
 * under "Unreleased changes" in CHANGELOG.md links no page of the documentation site, because a later build of
 * the site may drop the page.
 *
 * The anchor a heading gets depends on the platform of the Git host, as "Git hosts" in
 * `.claude/rules/common/documentation.md` says, and so does the check of every anchor and of every table of
 * contents.
 *
 * A repository has a documentation site when the root holds `docgen.json`, as "Documentation site" in
 * `.claude/rules/common/documentation.md` says. The site renders the pages under docs/, so the tool then checks
 * them against the rules of that section:
 *   - DocsIndex is not checked, because the site has no index, and DocsToc is not, because a page has no table
 *     of contents;
 *   - DocsTemplate is a page without a title or without a summary paragraph, a hand-written table of contents,
 *     a heading below level 4, or an "Overview" section;
 *   - the anchor of a heading of a page follows GitHub's rules on every Git host, because the generator computes it.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

const string Usage = "Usage: dotnet run .claude/tools/lint-docs.cs [<root>]";
const string IndexFileName = "README.md";
const string IndexPath = "docs/" + IndexFileName;
const string HomePagePath = "docs/index.md";
const string DocgenConfigPath = "docgen.json";
const string SiteContentRoot = "docs";
const string FolderFileName = "_folder.yml";
const string PreviewFolderName = "preview";
const string ReadmePath = "README.md";
const string ChangelogPath = "CHANGELOG.md";
const string DisableMarker = "<!-- markdownlint-disable MD036 -->";
const string TocTitle = "**Table of contents**";
const string EnableMarker = "<!-- markdownlint-enable MD036 -->";
const string Ruler = "---";
const string UnreleasedHeading = "Unreleased changes";
const string BreakingChangePrefix = "- **BREAKING CHANGE**:";
const int MaxSentenceWords = 25;

// The language tags `.claude/rules/common/documentation.md` admits on a fenced block.
string[] fenceTags = ["csharp", "json", "jsonc", "markdown", "powershell", "shell", "text", "xml", "yaml"];

// Globs of the informal documents that a name identifies: the ones "Register" in
// `.claude/rules/common/documentation.md` names, plus the files `ProjectFacts.cs` adds. The sentence check leaves
// an informal document alone.
string[] informalDocuments = [
    "**/README.md",
    "**/NuGet-README.md",
    .. ProjectFacts.Documentation.InformalDocuments,
];

if (args.Length > 1)
{
    Console.Error.WriteLine(Usage);
    return 2;
}

string[] gitHosts = ["GitHub", "GitLab", "Forgejo"];
if (!gitHosts.Contains(ProjectFacts.GitHost, StringComparer.Ordinal))
{
    Console.Error.WriteLine($"ProjectFacts.GitHost is \"{ProjectFacts.GitHost}\", and the platforms are GitHub, GitLab, and Forgejo.");
    return 2;
}

var rootGiven = args.Length == 1;
var root = Path.GetFullPath(rootGiven ? args[0] : Environment.CurrentDirectory);
if (!Directory.Exists(root))
{
    Console.Error.WriteLine($"{root} is not a directory.");
    Console.Error.WriteLine(Usage);
    return 2;
}

var docsDirectory = Path.Combine(root, "docs");
var hasDocs = Directory.Exists(docsDirectory);
if (!hasDocs)
{
    Console.Error.WriteLine("info: no docs/ directory: skipping the pages and the index.");
}

// The repository has a documentation site when the root holds docgen.json. The site then renders the pages
// under docs/, and "Documentation site" in the rules replaces the index and the page template.
string? siteContentRoot;
try
{
    siteContentRoot = ReadSiteContentRoot(Path.Combine(root, DocgenConfigPath));
}
catch (JsonException exception)
{
    Console.Error.WriteLine($"{DocgenConfigPath} is not valid JSON: {exception.Message}");
    return 2;
}

var hasSite = siteContentRoot is not null;
var frontMatterTitleRegex = new Regex(@"^title\s*:\s*\S", RegexOptions.CultureInvariant);
var frontMatterInformalRegex = new Regex(@"^x-informal\s*:\s*true\s*$", RegexOptions.CultureInvariant);

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
var linkTextRegex = new Regex(@"!?\[(?<text>[^\]]*)\]\([^)]*\)", RegexOptions.CultureInvariant);
var lineMarkerRegex = new Regex(@"^\s*(?:[-*+]|\d+\.)\s+|^\s*>\s*(?:\[!\w+\]\s*)?", RegexOptions.CultureInvariant);
var sentenceBreakRegex = new Regex(@"[.!?]\s+(?=[A-Z0-9(])", RegexOptions.CultureInvariant);
var nestedListRegex = new Regex(@"^\s+(?:[-*+]|\d+\.)\s", RegexOptions.CultureInvariant);

var findings = new List<(string File, int Line, int Column, string Code, string Message)>();

if (siteContentRoot is not null)
{
    if (!string.Equals(siteContentRoot, SiteContentRoot, StringComparison.Ordinal))
    {
        var shownRoot = siteContentRoot.Length > 0 ? $"\"{siteContentRoot}\"" : "not set";
        var message = $"content.root is {shownRoot}, and the content root of a documentation site is {SiteContentRoot}";
        Report(DocgenConfigPath, 1, 1, "DocsSite", message);
    }

    if (!File.Exists(Path.Combine(root, HomePagePath)))
    {
        Report(DocgenConfigPath, 1, 1, "DocsSite", $"the site has no home page: {HomePagePath} does not exist");
    }

    if (File.Exists(Path.Combine(root, IndexPath)))
    {
        Report(IndexPath, 1, 1, "DocsSite", $"README.md is a page of the site here, and the home page is {HomePagePath}");
    }
}

// The documentation set, as relative paths with forward slashes. It holds no page when there is no docs/
// directory.
var pages = new List<string>();
if (hasDocs)
{
    pages.AddRange(Directory.EnumerateFiles(docsDirectory, "*.md", SearchOption.AllDirectories)
        .Select(path => ToRelative(root, path))
        .Order(StringComparer.Ordinal));
}

// README.md and CHANGELOG.md are members of the set by name, so an absent one is named on standard error. A
// project directory holds a NuGet-README.md only when the project is packaged, so an absent one is not news.
string[] namedFiles = [ReadmePath, ChangelogPath];
var files = new List<string>();
foreach (var named in namedFiles)
{
    if (File.Exists(Path.Combine(root, named)))
    {
        files.Add(named);
    }
    else
    {
        Console.Error.WriteLine($"info: no {named}: skipping the file.");
    }
}

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

// File and folder names under docs/. The sequence is empty when there is no docs/ directory.
IEnumerable<string> docsEntries = hasDocs
    ? Directory.EnumerateFileSystemEntries(docsDirectory, "*", SearchOption.AllDirectories)
    : [];
foreach (var entry in docsEntries)
{
    var relative = ToRelative(root, entry);
    var segments = relative.Split('/');
    var name = segments[^1];
    if (Directory.Exists(entry))
    {
        if (!kebabRegex.IsMatch(name))
        {
            Report(relative, 1, 1, "DocsFileName", $"folder name \"{name}\" is not kebab-case");
        }

        // The generator names a folder of pages in the sidebar by the title of its _folder.yml.
        var folderFile = Path.Combine(entry, FolderFileName);
        var holdsPages = Directory.EnumerateFiles(entry, "*.md").Any();
        var hasTitle = File.Exists(folderFile) && File.ReadLines(folderFile).Any(frontMatterTitleRegex.IsMatch);
        if (hasSite && holdsPages && !hasTitle)
        {
            Report(relative, 1, 1, "DocsSite", $"folder \"{relative}\" holds pages, and no {FolderFileName} with a title");
        }

        // The preview part of the site is the `preview` folder of the stable part, so no folder of pages takes
        // that name, or that path in the site.
        var isPreviewFolder = string.Equals(name, PreviewFolderName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(FolderSlug(folderFile, name), PreviewFolderName, StringComparison.OrdinalIgnoreCase);
        if (hasSite && isPreviewFolder)
        {
            Report(relative, 1, 1, "DocsSite", $"folder \"{relative}\" takes the path of the preview part of the site");
        }

        continue;
    }

    var isPage = name.EndsWith(".md", StringComparison.Ordinal) && !string.Equals(name, IndexFileName, StringComparison.Ordinal);
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
var frontMatterLengthByFile = new Dictionary<string, int>(StringComparer.Ordinal);
foreach (var file in files)
{
    _ = Load(file);
}

foreach (var file in files)
{
    var lines = linesByFile[file];
    var inFence = inFenceByFile[file];
    var frontMatterLength = frontMatterLengthByFile[file];
    var isSitePage = IsSitePage(file);
    var isPage = !hasSite && file.StartsWith("docs/", StringComparison.Ordinal) && !IsIndex(file);
    var isChangelog = string.Equals(file, ChangelogPath, StringComparison.Ordinal);
    var checkTodo = !MatchesAny(ProjectFacts.Documentation.TodoExemptFiles, file);
    var checkSentences = !IsInformal(file);
    var openRegions = new Dictionary<string, int>(StringComparer.Ordinal);
    var releasedSection = string.Empty;
    var inUnreleased = false;
    var plainBulletSeen = false;
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

        // A line of the front matter is YAML, and no other check reads it.
        if (i < frontMatterLength)
        {
            continue;
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

        if (isChangelog)
        {
            var heading = headingRegex.Match(line);
            if (heading.Success && heading.Groups["hashes"].Value.Length == 2)
            {
                // A released section is headed by a link to the release, and the version is its text.
                var text = heading.Groups["text"].Value;
                var versionLink = Regex.Match(text, @"^\[(?<version>[^\]]+)\]", RegexOptions.CultureInvariant);
                var shownSection = versionLink.Success ? versionLink.Groups["version"].Value : text;
                inUnreleased = string.Equals(text, UnreleasedHeading, StringComparison.Ordinal);
                releasedSection = inUnreleased ? string.Empty : shownSection;
            }

            // A bullet under "Unreleased changes" is one sentence, so it holds no nested list, and the breaking
            // changes of a subsection come before its other bullets. A heading starts a subsection.
            if (heading.Success)
            {
                plainBulletSeen = false;
            }
            else if (inUnreleased && nestedListRegex.IsMatch(line))
            {
                Report(file, lineNumber, 1, "DocsChangelog", "a bullet under \"Unreleased changes\" holds a nested list");
            }
            else if (inUnreleased && line.StartsWith("- ", StringComparison.Ordinal))
            {
                var isBreaking = line.StartsWith(BreakingChangePrefix, StringComparison.Ordinal);
                if (isBreaking && plainBulletSeen)
                {
                    Report(file, lineNumber, 1, "DocsChangelog", "a breaking change follows a plain bullet in its subsection");
                }

                plainBulletSeen |= !isBreaking;
            }
        }

        // The released sections of the changelog hold lines over the limit, and are never edited.
        var isReleased = isChangelog && releasedSection.Length > 0;
        if (checkSentences && !isReleased)
        {
            CheckSentence(file, lineNumber, line);
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

        var prose = codeSpanRegex.Replace(line, match => new string(' ', match.Length));
        foreach (Match match in linkRegex.Matches(prose))
        {
            var target = match.Groups["target"].Value;
            var column = match.Index + 1;
            if (releasedSection.Length > 0 && target is not ['#', ..] && !schemeRegex.IsMatch(target))
            {
                var message = $"released section \"{releasedSection}\" links \"{target}\" by relative path";
                Report(file, lineNumber, column, "DocsChangelog", message);
            }

            if (inUnreleased && IsSiteLink(target))
            {
                var message = $"a bullet under \"Unreleased changes\" links \"{target}\", in the documentation site";
                Report(file, lineNumber, column, "DocsChangelog", message);
            }

            CheckLink(file, lineNumber, column, target, targets);
        }
    }

    foreach (var (regionName, startLine) in openRegions)
    {
        Report(file, startLine, 1, "DocsRegion", $"region \"{regionName}\" does not end");
    }

    if (isSitePage)
    {
        CheckSitePage(file);
    }
    else if (isPage)
    {
        CheckTemplate(file);
    }
}

// A README.md under docs/ is the index of its directory. Every directory that holds a page, at any depth, has
// one. An index links every page of its directory and the index of every subfolder, and has no table of
// contents because it is one. A documentation site has no index.
var pageDirectories = pages
    .Where(page => !hasSite && !IsIndex(page))
    .SelectMany(AncestorDirectories)
    .Distinct(StringComparer.Ordinal)
    .Order(StringComparer.Ordinal)
    .ToList();
foreach (var directory in pageDirectories)
{
    var index = $"{directory}/{IndexFileName}";
    if (!linkTargetsByFile.TryGetValue(index, out var indexTargets))
    {
        Report(directory, 1, 1, "DocsIndex", $"folder \"{directory}\" holds a page, and no {IndexFileName}");
        continue;
    }

    var tocLine = Array.IndexOf(linesByFile[index], TocTitle);
    if (tocLine >= 0)
    {
        Report(index, tocLine + 1, 1, "DocsIndex", "the index has a table of contents, and it is one");
    }

    var ownPages = pages.Where(page => !IsIndex(page) && string.Equals(DirectoryOf(page), directory, StringComparison.Ordinal));
    var subfolderIndexes = pageDirectories
        .Where(subfolder => string.Equals(DirectoryOf(subfolder), directory, StringComparison.Ordinal))
        .Select(subfolder => $"{subfolder}/{IndexFileName}")
        .Where(linkTargetsByFile.ContainsKey);
    foreach (var expected in ownPages.Concat(subfolderIndexes))
    {
        if (!indexTargets.Contains(expected))
        {
            Report(index, 1, 1, "DocsIndex", $"the index does not link \"{expected}\"");
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

// Reads a file, marks the lines inside fenced blocks, and lists its headings with their anchors on the Git host.
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

    var isSitePage = IsSitePage(file);
    var frontMatterLength = FrontMatterLength(lines);
    for (var i = frontMatterLength; i < lines.Length; i++)
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
        var slug = Slug(text, isSitePage);
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
    frontMatterLengthByFile[file] = frontMatterLength;
    return true;
}

// Whether the documentation site renders a file: every file under docs/, in a repository that has the site.
bool IsSitePage(string file)
{
    return hasSite && file.StartsWith("docs/", StringComparison.Ordinal);
}

// Whether a file of the documentation set is an informal document: by its name, or by its front matter.
bool IsInformal(string file)
{
    var frontMatter = linesByFile[file].Take(frontMatterLengthByFile[file]);
    return MatchesAny(informalDocuments, file) || frontMatter.Any(frontMatterInformalRegex.IsMatch);
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

// A prose line holds one sentence of at most MaxSentenceWords words. A blank line, a heading, a table row, and a
// line of HTML are not prose. A code span counts as one word, and a link as the words of its text. The list
// marker, the blockquote marker, and the alert marker of a line are not words.
void CheckSentence(string file, int lineNumber, string line)
{
    var trimmed = line.Trim();
    var isProse = trimmed is [not ('#' or '|' or '<'), ..];
    if (!isProse)
    {
        return;
    }

    var prose = codeSpanRegex.Replace(line, "CODE");
    prose = linkTextRegex.Replace(prose, "${text}");
    prose = lineMarkerRegex.Replace(prose, string.Empty);
    var words = prose.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    if (words > MaxSentenceWords)
    {
        Report(file, lineNumber, 1, "DocsSentence", $"the line holds {words} words, and a sentence holds at most {MaxSentenceWords}");
    }

    if (sentenceBreakRegex.IsMatch(prose))
    {
        Report(file, lineNumber, 1, "DocsSentence", "the line holds more than one sentence");
    }
}

void CheckLink(string file, int lineNumber, int column, string target, HashSet<string> targets)
{
    if (target is ['#', ..])
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

// A page of the documentation site has a title, then a summary paragraph, and no table of contents. The title is
// `title` in the front matter, or a level 1 heading on the first line after it.
void CheckSitePage(string file)
{
    var lines = linesByFile[file];
    foreach (var (line, level, text, _) in headingsByFile[file])
    {
        CheckHeading(file, line, level, text);
    }

    var tocLine = Array.IndexOf(lines, TocTitle);
    if (tocLine >= 0)
    {
        Report(file, tocLine + 1, 1, "DocsTemplate", "a hand-written table of contents: the site lists the headings of a page");
    }

    var frontMatterLength = frontMatterLengthByFile[file];
    var hasFrontMatterTitle = lines.Take(frontMatterLength).Any(frontMatterTitleRegex.IsMatch);
    var index = SkipBlankLines(lines, frontMatterLength);
    if (!hasFrontMatterTitle)
    {
        if (index >= lines.Length || !lines[index].StartsWith("# ", StringComparison.Ordinal))
        {
            const string Message = "the page has no title: neither `title` in a front matter, nor a level 1 heading on the first line";
            Report(file, index + 1, 1, "DocsTemplate", Message);
            return;
        }

        index = SkipBlankLines(lines, index + 1);
    }

    var summary = index < lines.Length ? lines[index] : string.Empty;
    var isProse = summary is [not ('#' or '<' or '>' or '|'), ..]
        && !string.Equals(summary, Ruler, StringComparison.Ordinal);
    if (!isProse)
    {
        Report(file, index + 1, 1, "DocsTemplate", "no summary paragraph after the title");
    }
}

void CheckTemplate(string file)
{
    var lines = linesByFile[file];
    var headings = headingsByFile[file];
    foreach (var (line, level, text, _) in headings)
    {
        CheckHeading(file, line, level, text);
        if (level == 2)
        {
            CheckRuler(file, lines, line, text);
        }
    }

    // In a page with a front matter, the template starts after it.
    var frontMatterLength = frontMatterLengthByFile[file];
    var titleIndex = frontMatterLength > 0 ? SkipBlankLines(lines, frontMatterLength) : 0;
    if (titleIndex >= lines.Length || !lines[titleIndex].StartsWith("# ", StringComparison.Ordinal))
    {
        Report(file, titleIndex + 1, 1, "DocsTemplate", "the first line is not the title, a level 1 heading");
        return;
    }

    if (!Expect(file, lines, titleIndex + 1, string.Empty))
    {
        return;
    }

    var summaryIndex = titleIndex + 2;
    var summary = lines.Length > summaryIndex ? lines[summaryIndex] : string.Empty;
    var isProse = summary is [not ('#' or '<'), ..]
        && !string.Equals(summary, Ruler, StringComparison.Ordinal);
    if (!isProse)
    {
        Report(file, summaryIndex + 1, 1, "DocsTemplate", "no summary paragraph between the title and the table of contents");
        return;
    }

    var summaryEnd = summaryIndex;
    while (summaryEnd < lines.Length && lines[summaryEnd].Length > 0)
    {
        summaryEnd++;
    }

    // One or more blockquotes, such as alerts that apply to the whole page, may follow the summary
    // paragraph. Each is a block of lines starting with ">", after a blank line.
    while (summaryEnd + 1 < lines.Length && lines[summaryEnd + 1] is ['>', ..])
    {
        summaryEnd++;
        while (summaryEnd < lines.Length && lines[summaryEnd].Length > 0)
        {
            summaryEnd++;
        }
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

// The rules on headings that hold for every page, on the Git host and on the documentation site.
void CheckHeading(string file, int line, int level, string text)
{
    if (level > 4)
    {
        Report(file, line, 1, "DocsTemplate", $"the heading \"{new string('#', level)} {text}\" is below level 4");
    }

    if (level == 2 && string.Equals(text, "Overview", StringComparison.Ordinal))
    {
        const string Message = "an \"Overview\" section: put its content in the summary paragraph, or under a heading that names it";
        Report(file, line, 1, "DocsTemplate", Message);
    }
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

// The content root that docgen.json states for the documentation site, without a leading "./" and a trailing
// slash. Null when the file does not exist, which means the repository has no site. An empty string when the
// file states no content root. The generator accepts comments and trailing commas in the file.
static string? ReadSiteContentRoot(string path)
{
    if (!File.Exists(path))
    {
        return null;
    }

    var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    using var document = JsonDocument.Parse(File.ReadAllText(path), options);
    var config = document.RootElement;
    var content = config.ValueKind == JsonValueKind.Object && config.TryGetProperty("content", out var contentProperty)
        ? contentProperty
        : default;
    var hasRoot = content.ValueKind == JsonValueKind.Object
        && content.TryGetProperty("root", out var rootProperty)
        && rootProperty.ValueKind == JsonValueKind.String;
    var contentRoot = hasRoot ? content.GetProperty("root").GetString() ?? string.Empty : string.Empty;
    contentRoot = contentRoot.Replace('\\', '/').TrimEnd('/');
    return contentRoot.StartsWith("./", StringComparison.Ordinal) ? contentRoot[2..] : contentRoot;
}

// The number of lines of the YAML front matter of a page, both "---" lines included, or 0 when the page has none.
static int FrontMatterLength(string[] lines)
{
    if (lines.Length == 0 || !string.Equals(lines[0], "---", StringComparison.Ordinal))
    {
        return 0;
    }

    var end = Array.IndexOf(lines, "---", 1);
    return end < 0 ? 0 : end + 1;
}

// The path segment the generator gives a folder of pages, in the case of its source: the `slug` of its
// _folder.yml, else its `title` slugified, else its name slugified without a number prefix. The generator
// lowercases the segment, so a caller compares it without case.
static string FolderSlug(string folderFile, string folderName)
{
    string[] lines = File.Exists(folderFile) ? File.ReadAllLines(folderFile) : [];
    var slug = YamlValue(lines, "slug");
    if (slug.Length > 0)
    {
        return slug;
    }

    var title = YamlValue(lines, "title");
    var source = title.Length > 0 ? title : Regex.Replace(folderName, @"^\d+-", string.Empty, RegexOptions.CultureInvariant);
    var slugified = Regex.Replace(source, "[^A-Za-z0-9]+", "-", RegexOptions.CultureInvariant);
    return slugified.Trim('-');
}

// The value of a top-level key of a YAML file, without quotes, or an empty string when the file has no such key.
static string YamlValue(string[] lines, string key)
{
    var prefix = key + ":";
    var line = lines.FirstOrDefault(candidate => candidate.StartsWith(prefix, StringComparison.Ordinal));
    return line is null ? string.Empty : line[prefix.Length..].Trim().Trim('"', '\'');
}

static int SkipBlankLines(string[] lines, int index)
{
    while (index < lines.Length && lines[index].Length == 0)
    {
        index++;
    }

    return index;
}

// Whether a link target is the documentation site, or a page of it. A repository without a site states an empty
// URL, and no target is one. The host name of a URL has no case, so the comparison has none. The URL of the
// site ends with a slash, and a link to the site may leave the slash out.
static bool IsSiteLink(string target)
{
    var siteUrl = ProjectFacts.Documentation.SiteUrl;
    var isSite = string.Equals(target, siteUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    return siteUrl.Length > 0 && (isSite || target.StartsWith(siteUrl, StringComparison.OrdinalIgnoreCase));
}

// Whether a file under docs/ is the index of its directory.
static bool IsIndex(string file)
{
    return file.StartsWith("docs/", StringComparison.Ordinal) && file.EndsWith("/" + IndexFileName, StringComparison.Ordinal);
}

// The directories that hold a file, from its own up to the first segment of its path, as relative paths.
static IEnumerable<string> AncestorDirectories(string file)
{
    for (var directory = DirectoryOf(file); directory.Length > 0; directory = DirectoryOf(directory))
    {
        yield return directory;
    }
}

// The directory of a relative path with forward slashes, or an empty string when the path has one segment.
static string DirectoryOf(string path)
{
    var slash = path.LastIndexOf('/', StringComparison.Ordinal);
    return slash < 0 ? string.Empty : path[..slash];
}

// Whether a relative path, with forward slashes, matches one of the globs. `ProjectFacts.cs` states the glob syntax.
static bool MatchesAny(IEnumerable<string> globs, string relativePath)
{
    return globs.Any(glob => Regex.IsMatch(relativePath, GlobToRegex(glob), RegexOptions.CultureInvariant));
}

static string GlobToRegex(string glob)
{
    var builder = new StringBuilder("^");
    for (var i = 0; i < glob.Length; i++)
    {
        var ch = glob[i];
        if (ch == '*' && i + 1 < glob.Length && glob[i + 1] == '*')
        {
            i++;
            if (i + 1 < glob.Length && glob[i + 1] == '/')
            {
                // `**/` matches zero or more whole segments, so that `**/README.md` also matches the root README.md.
                i++;
                _ = builder.Append("(?:.*/)?");
            }
            else
            {
                _ = builder.Append(".*");
            }
        }
        else if (ch == '*')
        {
            _ = builder.Append("[^/]*");
        }
        else if (ch == '?')
        {
            _ = builder.Append("[^/]");
        }
        else
        {
            _ = builder.Append(Regex.Escape(ch.ToString()));
        }
    }

    return builder.Append('$').ToString();
}

static string ToRelative(string root, string path)
{
    return Path.GetRelativePath(root, path).Replace('\\', '/');
}

// The anchor the Git host gives a heading, as "Git hosts" in `.claude/rules/common/documentation.md` describes.
// Every platform lowercases the text and turns a space into a hyphen. Markdown formatting is punctuation to every
// platform, so the backticks of a code span go, and the text of a link stays while its target goes. The platforms
// differ on the other punctuation, and the two functions below hold the two algorithms. The documentation site
// renders its own pages, and the generator gives a heading the anchor of GitHub's rules, whatever the Git host.
static string Slug(string headingText, bool isSitePage)
{
    var text = Regex.Replace(headingText, @"\[([^\]]*)\]\([^)]*\)", "$1", RegexOptions.CultureInvariant);
    var useForgejoRules = !isSitePage && string.Equals(ProjectFacts.GitHost, "Forgejo", StringComparison.Ordinal);
    return useForgejoRules ? ForgejoSlug(text) : GitHubSlug(text);
}

// GitHub and GitLab keep letters, digits, hyphens and underscores, and drop every other character. GitLab renders
// through comrak, whose anchorizer follows GitHub's rules.
static string GitHubSlug(string text)
{
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

// Forgejo keeps letters, digits and underscores. A run of any other characters between two kept characters becomes
// one hyphen, and such a run at either end is dropped. This is `CleanValue` of its `markup/common` package.
static string ForgejoSlug(string text)
{
    var builder = new StringBuilder();
    var needsHyphen = false;
    foreach (var ch in text)
    {
        var lower = char.ToLowerInvariant(ch);
        if (char.IsLetterOrDigit(lower) || lower == '_')
        {
            if (needsHyphen && builder.Length > 0)
            {
                _ = builder.Append('-');
            }

            _ = builder.Append(lower);
            needsHyphen = false;
        }
        else
        {
            needsHyphen = true;
        }
    }

    return builder.ToString();
}
