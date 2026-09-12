// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Buildvana.Core.Testing;

// docs/sdk-diagnostics.md, pinned against the codes Buildvana SDK raises: the EvaluationError and
// EvaluationWarning items and the Error and Warning tasks of the .props and .targets files, the messages of the
// Strings class of the tasks project, and the descriptors of the source generators. A code is raised where a
// string literal starts with it, which leaves out the comments that mention one. The repository root reaches the
// test through an assembly metadata item the test project supplies, as SdkPropsFixture reads the real Sdk.props.
internal sealed partial class SdkDiagnosticsPageTests
{
    private const string CodePrefix = "BVSDK";

    private static readonly string RepositoryRoot = typeof(SdkDiagnosticsPageTests).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .First(static attribute => attribute.Key == "RepositoryRoot")
        .Value!;

    // Every code the SDK raises is on the page, and every code on the page is raised.
    [Test]
    public async Task Page_ListsTheCodesTheSdkRaises()
    {
        var page = LoadPage();
        var listed = page.GetHeadings(2).SelectMany(heading => page.GetTableColumn(heading, "Code"));

        await Assert.That(Join(listed)).IsEqualTo(Join(RaisedCodes()));
    }

    // Every section heading names a range, and every row of its table sits in that range.
    [Test]
    public async Task Page_ListsEachCodeUnderItsRange()
    {
        var page = LoadPage();
        List<string> problems = [];
        foreach (var heading in page.GetHeadings(2))
        {
            var range = RangeRegex().Match(heading);
            foreach (var code in page.GetTableColumn(heading, "Code"))
            {
                if (!IsInRange(code, range))
                {
                    problems.Add($"{code} under \"{heading}\"");
                }
            }
        }

        await Assert.That(string.Join("; ", problems)).IsEqualTo(string.Empty);
    }

    private static DocumentationPage LoadPage()
        => DocumentationPage.Load(Path.Combine(RepositoryRoot, "docs", "sdk-diagnostics.md"));

    private static bool IsInRange(string code, Match range)
    {
        if (!range.Success)
        {
            return false;
        }

        var number = int.Parse(code[CodePrefix.Length..], CultureInfo.InvariantCulture);
        var low = int.Parse(range.Groups["low"].Value, CultureInfo.InvariantCulture);
        var high = int.Parse(range.Groups["high"].Value, CultureInfo.InvariantCulture);
        return number >= low && number <= high;
    }

    private static HashSet<string> RaisedCodes()
    {
        var files = SourceFiles("Buildvana.Sdk", ".props", ".targets")
            .Concat(SourceFiles("Buildvana.Sdk.Tasks", ".cs"))
            .Concat(SourceFiles("Buildvana.Sdk.SourceGenerators", ".cs"));
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            foreach (Match match in RaisedCodeRegex().Matches(File.ReadAllText(file)))
            {
                _ = codes.Add(match.Value);
            }
        }

        return codes;
    }

    // The source files of a project under src/ with one of the given extensions, leaving out the build output.
    private static IEnumerable<string> SourceFiles(string project, params string[] extensions)
    {
        var directory = Path.Combine(RepositoryRoot, "src", project);
        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(path => extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Where(path => !IsBuildOutput(Path.GetRelativePath(directory, path)));
    }

    private static bool IsBuildOutput(string relativePath)
    {
        var first = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        return first is "bin" or "obj";
    }

    private static string Join(IEnumerable<string> codes) => string.Join(", ", codes.Order(StringComparer.Ordinal));

    // A code at the start of a string literal, followed by the closing quote or by the colon of a message.
    [GeneratedRegex("""(?<=")BVSDK\d{4}(?=[":])""", RegexOptions.CultureInvariant)]
    private static partial Regex RaisedCodeRegex();

    [GeneratedRegex(@"\((?<low>\d{4})-(?<high>\d{4})\)$", RegexOptions.CultureInvariant)]
    private static partial Regex RangeRegex();
}
