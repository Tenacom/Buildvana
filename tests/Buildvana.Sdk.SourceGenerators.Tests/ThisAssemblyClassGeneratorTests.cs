// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using Buildvana.Sdk.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal sealed class ThisAssemblyClassGeneratorTests
{
    private const string ConstantsFilePath = "/obj/Test.ThisAssemblyConstants.txt";

    private static readonly IReadOnlyDictionary<string, string> ConstantsFileMetadata = new Dictionary<string, string>
    {
        ["build_metadata.AdditionalFiles.BV_ItemType"] = "ThisAssemblyConstants",
    };

    [Test]
    public async Task NoAdditionalFiles_GeneratesNothing()
    {
        var result = RunGenerator(constantsContent: null);
        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(0);
    }

    [Test]
    public async Task UntaggedConstantsFile_GeneratesNothing()
    {
        var result = RunGenerator("Answer=42\n", tagFile: false);
        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(0);
    }

    [Test]
    public async Task EmptyConstantsFile_GeneratesNothing()
    {
        var result = RunGenerator(string.Empty);
        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Constants_GenerateClassWithExpectedShape()
    {
        var result = RunGenerator("Answer=42\n");
        var source = GetSingleGeneratedSource(result);
        await Assert.That(source).Contains("[global::System.Runtime.CompilerServices.CompilerGenerated]");
        await Assert.That(source).Contains("[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]");
        await Assert.That(source).Contains("internal static partial class ThisAssembly");
        await Assert.That(source).Contains("public const int Answer = 42;");
        await Assert.That(source).DoesNotContain("namespace");
        await AssertCompiles(result).ConfigureAwait(false);
    }

    [Test]
    public async Task Constants_OfAllTypes_GenerateExpectedFields()
    {
        const string content
            = "ByteValue=byte%3A200\n"
            + "ShortValue=short%3A-5\n"
            + "IntValue=42\n"
            + "LongValue=long%3A42\n"
            + "BoolValue=bool%3Atrue\n"
            + "StringValue=string%3AHello%20World\n"
            + "NullValue=\n";

        var result = RunGenerator(content);
        var source = GetSingleGeneratedSource(result);
        await Assert.That(source).Contains("public const byte ByteValue = 200;");
        await Assert.That(source).Contains("public const short ShortValue = -5;");
        await Assert.That(source).Contains("public const int IntValue = 42;");
        await Assert.That(source).Contains("public const long LongValue = 42L;");
        await Assert.That(source).Contains("public const bool BoolValue = true;");
        await Assert.That(source).Contains("public const string StringValue = \"Hello World\";");
        await Assert.That(source).Contains("public const string? NullValue = null;");
        await AssertCompiles(result).ConfigureAwait(false);
    }

    [Test]
    public async Task Constants_WithSpecialCharacters_GenerateEscapedLiterals()
    {
        // Value is "a=b" + newline + quote + backslash + tab, percent-encoded as the task would write it.
        var result = RunGenerator("Tricky=a%3Db%0A%22%5C%09\n");
        var source = GetSingleGeneratedSource(result);
        await Assert.That(source).Contains("public const string Tricky = \"a=b\\n\\\"\\\\\\t\";");
        await AssertCompiles(result).ConfigureAwait(false);
    }

    [Test]
    public async Task CustomClassNameAndNamespace_AreHonored()
    {
        var globalOptions = new Dictionary<string, string>
        {
            ["build_property.ThisAssemblyClassName"] = "MyAssemblyInfo",
            ["build_property.ThisAssemblyClassNamespace"] = "Some.Name.Space",
        };
        var result = RunGenerator("Answer=42\n", globalOptions);
        var source = GetSingleGeneratedSource(result);
        await Assert.That(source).Contains("namespace Some.Name.Space");
        await Assert.That(source).Contains("internal static partial class MyAssemblyInfo");
        await AssertCompiles(result).ConfigureAwait(false);
    }

    [Test]
    public async Task InvalidConstantValue_ReportsDiagnosticAndGeneratesNothing()
    {
        var result = RunGenerator("Bad=int%3Afoo\nGood=42\n");
        await Assert.That(result.RunResult.GeneratedTrees.Length).IsEqualTo(0);
        var diagnostics = result.RunResult.Results[0].Diagnostics;
        await Assert.That(diagnostics.Length).IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("BVSDK2301");
        await Assert.That(diagnostics[0].GetMessage(CultureInfo.InvariantCulture)).Contains("'Bad'");
        await Assert.That(diagnostics[0].GetMessage(CultureInfo.InvariantCulture)).Contains("'int:foo'");
    }

    private static (GeneratorDriverRunResult RunResult, Compilation OutputCompilation) RunGenerator(
        string? constantsContent,
        IReadOnlyDictionary<string, string>? globalOptions = null,
        bool tagFile = true)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: GetReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var additionalTexts = constantsContent is null
            ? []
            : new AdditionalText[] { new InMemoryAdditionalText(ConstantsFilePath, constantsContent) };

        var fileOptions = new Dictionary<string, IReadOnlyDictionary<string, string>>();
        if (tagFile)
        {
            fileOptions[ConstantsFilePath] = ConstantsFileMetadata;
        }

        var optionsProvider = new TestAnalyzerConfigOptionsProvider(
            globalOptions ?? new Dictionary<string, string>(),
            fileOptions);

        var driver = CSharpGeneratorDriver.Create(
            [new ThisAssemblyClassGenerator().AsSourceGenerator()],
            additionalTexts,
            optionsProvider: optionsProvider);

        var runResult = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _).GetRunResult();
        return (runResult, outputCompilation);
    }

    private static string GetSingleGeneratedSource((GeneratorDriverRunResult RunResult, Compilation OutputCompilation) result)
        => result.RunResult.GeneratedTrees.Single().ToString();

    private static async Task AssertCompiles((GeneratorDriverRunResult RunResult, Compilation OutputCompilation) result)
    {
        var errors = result.OutputCompilation
                           .GetDiagnostics()
                           .Where(d => d.Severity == DiagnosticSeverity.Error)
                           .ToArray();
        await Assert.That(errors).IsEmpty();
    }

    private static IEnumerable<MetadataReference> GetReferences()
    {
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        return trustedAssemblies
              .Where(p => Path.GetFileName(p) is "System.Runtime.dll" or "System.Private.CoreLib.dll" or "netstandard.dll")
              .Select(MetadataReference (p) => MetadataReference.CreateFromFile(p));
    }
}
