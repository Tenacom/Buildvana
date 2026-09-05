// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

/*
 * Checks a commit message against the rules that a tool can measure. The rules come from the "Commit messages"
 * section of `.claude/rules/workflow.md` and from `.claude/output-styles/simple-tech.md`.
 *
 * Run from the repo root, on the file that `git commit -F` will read:
 *   `dotnet run .claude/tools/lint-commit.cs <message-file>`
 *
 * The tool writes one `file(line): message` per finding to standard output. It exits 1 when there is any
 * finding, 0 when there is none, and 2 on a usage error. The checks are:
 *   - the subject is present, at most 72 characters long, and does not start with a word that announces what a
 *     document says now instead of what changed ("Say", "Tell", "Judge", "Clear");
 *   - the line after the subject is blank;
 *   - the body, trailers excluded, is one paragraph of at most six sentences;
 *   - a sentence has at most 25 words, holds no semicolon, and holds no dash;
 *   - no sentence uses a word from the banned list below.
 *
 * A backticked or double-quoted span counts as one word, and nothing inside it is checked. A paragraph whose
 * first line starts with a list marker is a list, and each marker line in it starts a new sentence. A marker at
 * the start of any other line is text. A trailer block is one or more `Key: value` lines at the end of the
 * message, with a blank line before it. A lone `Part of #123` line counts as a trailer too.
 *
 * What the tool cannot check stays with the reader: a coined name, a subject that names the rule instead of the
 * behavior that is gone, and a condition stated after its action.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

const int MaxSubjectLength = 72;
const int MaxWordsPerSentence = 25;
const int MaxSentencesPerParagraph = 6;

const string Usage = "Usage: dotnet run .claude/tools/lint-commit.cs <message-file>";

// A subject that starts with one of these announces what a document says now, or the rule the change obeys,
// instead of the behavior that is gone.
string[] announcingVerbs = ["Say", "Tell", "Judge", "Clear"];

// Metaphors and house verbs that name nothing in the repository. The first four are the style's own examples.
// The rest stood in, across past commits, for an exit code ("verdict", "judge"), for "keep" ("spare"), and for
// three properties listed once and never named again ("trio").
string[] bannedWords = [
    "load-bearing",
    "seam",
    "knob",
    "lift",
    "lifts",
    "lifted",
    "by construction",
    "on its owner's terms",
    "verdict",
    "judge",
    "judged",
    "judgement",
    "judgment",
    "spare",
    "spared",
    "spares",
    "trio",
];

if (args.Length != 1)
{
    Console.Error.WriteLine(Usage);
    return 2;
}

var path = args[0];
if (!File.Exists(path))
{
    Console.Error.WriteLine($"Cannot find {path}.");
    Console.Error.WriteLine(Usage);
    return 2;
}

// A space inside a banned phrase matches any whitespace, the newline between two lines of a paragraph included.
var bannedPattern = string.Join(
    "|",
    bannedWords.Select(word => string.Join(@"[ \t\n]+", word.Split(' ').Select(Regex.Escape))));
var bannedRegex = new Regex($@"\b(?:{bannedPattern})\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

// A trailer is what git calls one (`Key: value`), plus the issue reference this repository writes on its own line.
var trailerRegex = new Regex(
    @"^(?:[A-Za-z][A-Za-z0-9-]*: \S.*|(?:Part of|Fixes|Closes|Resolves) #\d+\.?)$",
    RegexOptions.CultureInvariant);

var lines = File.ReadAllLines(path);
var findings = new List<(int Line, string Message)>();

var subjectIndex = Array.FindIndex(lines, line => line.Trim().Length > 0);
if (subjectIndex < 0)
{
    findings.Add((1, "the message is empty"));
    return Finish(path, findings);
}

var subjectLine = subjectIndex + 1;
var subject = lines[subjectIndex].Trim();
if (subject.Length > MaxSubjectLength)
{
    findings.Add((subjectLine, $"the subject is {subject.Length} characters long (max {MaxSubjectLength})"));
}

var firstWord = subject.Split(' ', 2)[0];
if (announcingVerbs.Contains(firstWord, StringComparer.Ordinal))
{
    findings.Add((subjectLine, $"the subject starts with \"{firstWord}\": name what changed, not what is said now"));
}

CheckParagraph(findings, [(subjectLine, subject)], bannedRegex);

var hasUnblankSecondLine = subjectIndex + 1 < lines.Length && lines[subjectIndex + 1].Trim().Length > 0;
if (hasUnblankSecondLine)
{
    findings.Add((subjectIndex + 2, "the line after the subject is not blank"));
}

// Trailers are peeled off the end, as git reads them: a block of trailer lines with a blank line before it.
// A trailer-shaped line inside the last paragraph is body text. Whatever is left is the body.
var end = lines.Length;
while (end > subjectIndex + 1 && lines[end - 1].Trim().Length == 0)
{
    end--;
}

var trailerStart = end;
while (trailerStart > subjectIndex + 1 && trailerRegex.IsMatch(lines[trailerStart - 1].Trim()))
{
    trailerStart--;
}

var blankBeforeTrailers = trailerStart < end && lines[trailerStart - 1].Trim().Length == 0;
if (blankBeforeTrailers)
{
    end = trailerStart;
}

var paragraphs = new List<List<(int Line, string Text)>>();
List<(int Line, string Text)>? current = null;
for (var i = subjectIndex + 1; i < end; i++)
{
    var text = lines[i].Trim();
    if (text.Length == 0)
    {
        current = null;
        continue;
    }

    if (current is null)
    {
        current = [];
        paragraphs.Add(current);
    }

    current.Add((i + 1, text));
}

if (paragraphs.Count > 1)
{
    findings.Add((paragraphs[1][0].Line, $"the body has {paragraphs.Count} paragraphs: state the why in one"));
}

foreach (var paragraph in paragraphs)
{
    CheckParagraph(findings, paragraph, bannedRegex);
}

return Finish(path, findings);

static int Finish(string path, List<(int Line, string Message)> findings)
{
    foreach (var (line, message) in findings.OrderBy(finding => finding.Line))
    {
        Console.WriteLine($"{path}({line}): {message}");
    }

    Console.Error.WriteLine($"=== lint-commit: {findings.Count} finding(s) ===");
    return findings.Count > 0 ? 1 : 0;
}

static void CheckParagraph(
    List<(int Line, string Message)> findings,
    IReadOnlyList<(int Line, string Text)> paragraph,
    Regex bannedRegex)
{
    // Lines are joined with a newline, so that a list marker at the start of a line can be told from the rest.
    var builder = new StringBuilder();
    var lineStarts = new List<(int Offset, int Line)>();
    foreach (var (line, text) in paragraph)
    {
        if (builder.Length > 0)
        {
            _ = builder.Append('\n');
        }

        lineStarts.Add((builder.Length, line));
        _ = builder.Append(text);
    }

    var joined = builder.ToString();
    var guarded = Guard(joined);
    var isList = Regex.IsMatch(paragraph[0].Text, @"^(?:[-*]|\d+\.)[ \t]", RegexOptions.CultureInvariant);
    var sentences = SplitSentences(guarded, isList);
    if (sentences.Count > MaxSentencesPerParagraph)
    {
        var message = $"the paragraph has {sentences.Count} sentences (max {MaxSentencesPerParagraph})";
        findings.Add((paragraph[0].Line, message));
    }

    foreach (var (offset, length) in sentences)
    {
        var line = LineOf(lineStarts, offset);
        var sentence = guarded.Substring(offset, length);
        var shown = joined.Substring(offset, length).Replace('\n', ' ');
        var words = sentence.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries).Length;
        if (words > MaxWordsPerSentence)
        {
            findings.Add((line, $"{words} words in one sentence (max {MaxWordsPerSentence}): {shown}"));
        }

        if (sentence.Contains(';', StringComparison.Ordinal))
        {
            findings.Add((line, $"a semicolon joins two clauses, use a full stop: {shown}"));
        }

        if (HasDash(sentence))
        {
            findings.Add((line, $"a dash sets off an appositive, make it a sentence or drop it: {shown}"));
        }

        foreach (Match match in bannedRegex.Matches(sentence))
        {
            var word = match.Value.Replace('\n', ' ');
            findings.Add((line, $"\"{word}\" names nothing in the repository, describe the mechanism: {shown}"));
        }
    }
}

// Replaces every character inside a backticked or double-quoted span, and the periods of a few abbreviations,
// with a no-break space. The result has the same length as the input, so an offset into one is an offset into
// the other. A no-break space is not in the whitespace class the splitters below use, so a guarded span stays
// one word and never ends a sentence.
static string Guard(string text)
{
    var guarded = Regex.Replace(
        text,
        @"`[^`\n]*`|""[^""\n]*""",
        match => match.Value[0] + new string(' ', match.Length - 2) + match.Value[^1],
        RegexOptions.CultureInvariant);

    return Regex.Replace(
        guarded,
        @"\b(?:e\.g|i\.e|vs|etc)\.",
        match => match.Value.Replace('.', ' '),
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
}

// A sentence ends at a period, a question mark, or an exclamation mark that is followed by whitespace, with
// a closing quote or parenthesis allowed in between. In a list, a line that starts with a list marker also
// starts a sentence, and the marker is left out of it. `\s` is not used, because in .NET it matches the
// no-break space that Guard relies on.
static List<(int Offset, int Length)> SplitSentences(string guarded, bool isList)
{
    const string SentenceEnd = @"(?<=[.!?][""')]?)[ \t\n]+";
    const string ListMarker = @"(?:^|\n)[ \t]*(?:[-*]|\d+\.)[ \t]+";
    var boundaries = Regex.Matches(
        guarded,
        isList ? $"{ListMarker}|{SentenceEnd}" : SentenceEnd,
        RegexOptions.CultureInvariant);

    var sentences = new List<(int Offset, int Length)>();
    var start = 0;
    foreach (Match boundary in boundaries)
    {
        AddSentence(sentences, guarded, start, boundary.Index);
        start = boundary.Index + boundary.Length;
    }

    AddSentence(sentences, guarded, start, guarded.Length);
    return sentences;
}

static void AddSentence(List<(int Offset, int Length)> sentences, string guarded, int start, int end)
{
    while (start < end && char.IsWhiteSpace(guarded[start]))
    {
        start++;
    }

    if (start < end)
    {
        sentences.Add((start, end - start));
    }
}

static int LineOf(IReadOnlyList<(int Offset, int Line)> lineStarts, int offset)
{
    var line = lineStarts[0].Line;
    foreach (var (start, candidate) in lineStarts)
    {
        if (start > offset)
        {
            break;
        }

        line = candidate;
    }

    return line;
}

// An em dash or an en dash anywhere, or a hyphen standing alone: after whitespace or the start of the
// sentence, and before whitespace or its end. The whitespace may be the newline between two lines of a
// paragraph. A hyphen inside a word ("no-break") is not a dash.
static bool HasDash(string sentence)
{
    return Regex.IsMatch(sentence, @"[—–]|(?:^|[ \t\n])-(?:[ \t\n]|$)", RegexOptions.CultureInvariant);
}
