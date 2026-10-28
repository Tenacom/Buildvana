// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Testing;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

// Evaluates the real Module.targets of the StandardAnalyzers module, imported by a project of a temporary home
// directory that states the project type properties Buildvana SDK computes before the module runs.
// Evaluation-only: no targets are executed, so assertions are limited to properties and items.
internal sealed class StandardAnalyzersModuleTargetsTests
{
    private const string ProjectDirectory = "src/Test";
    private const string EnableNETAnalyzers = "EnableNETAnalyzers";
    private const string UseStyleCopAnalyzers = "UseStyleCopAnalyzers";
    private const string UsePublicApiAnalyzers = "UsePublicApiAnalyzers";
    private const string UseNullableExtendedAnalyzer = "UseNullableExtendedAnalyzer";
    private const string StyleCopAnalyzers = "StyleCop.Analyzers";
    private const string PublicApiAnalyzers = "Microsoft.CodeAnalysis.PublicApiAnalyzers";
    private const string NullableExtendedAnalyzer = "Nullable.Extended.Analyzer";

    // A project that is neither a no-target project nor a library project.
    private const string OrdinaryProject = """
        <BV_IsNoTargetsProject>false</BV_IsNoTargetsProject>
        <BV_IsLibraryProject>false</BV_IsLibraryProject>
        """;

    private const string NoTargetsProject = """
        <BV_IsNoTargetsProject>true</BV_IsNoTargetsProject>
        <BV_IsLibraryProject>false</BV_IsLibraryProject>
        """;

    private const string LibraryProject = """
        <BV_IsNoTargetsProject>false</BV_IsNoTargetsProject>
        <BV_IsLibraryProject>true</BV_IsLibraryProject>
        """;

    [Test]
    public async Task Evaluate_ByDefault_SetsTheCodeAnalysisDefaults()
    {
        using var home = new TempHome();
        var project = Evaluate(home, OrdinaryProject);
        await Assert.That(project.GetPropertyValue(EnableNETAnalyzers)).IsEqualTo("true");
        await Assert.That(project.GetPropertyValue("AnalysisLevel")).IsEqualTo("latest");
        await Assert.That(project.GetPropertyValue("AnalysisMode")).IsEqualTo("All");
        await Assert.That(project.GetPropertyValue("EnforceCodeStyleInBuild")).IsEqualTo("true");
    }

    [Test]
    public async Task Evaluate_AnalysisLevelAndAnalysisModeSet_KeepsBoth()
    {
        using var home = new TempHome();
        const string properties = OrdinaryProject + "<AnalysisLevel>9.0</AnalysisLevel><AnalysisMode>Recommended</AnalysisMode>";
        var project = Evaluate(home, properties);
        await Assert.That(project.GetPropertyValue("AnalysisLevel")).IsEqualTo("9.0");
        await Assert.That(project.GetPropertyValue("AnalysisMode")).IsEqualTo("Recommended");
    }

    [Test]
    [Arguments("false")]
    [Arguments("no")]
    [Arguments("Off")]
    [Arguments("!true")]
    public async Task Evaluate_EnforceCodeStyleInBuildMSBuildReadsAsFalse_StoresFalse(string value)
    {
        using var home = new TempHome();
        var project = Evaluate(home, OrdinaryProject + $"<EnforceCodeStyleInBuild>{value}</EnforceCodeStyleInBuild>");
        await Assert.That(project.GetPropertyValue("EnforceCodeStyleInBuild")).IsEqualTo("false");
    }

    [Test]
    [Arguments("true")]
    [Arguments("maybe")]
    public async Task Evaluate_EnforceCodeStyleInBuildOtherThanFalse_StoresTrue(string value)
    {
        using var home = new TempHome();
        var project = Evaluate(home, OrdinaryProject + $"<EnforceCodeStyleInBuild>{value}</EnforceCodeStyleInBuild>");
        await Assert.That(project.GetPropertyValue("EnforceCodeStyleInBuild")).IsEqualTo("true");
    }

    [Test]
    public async Task Evaluate_OrdinaryProject_ReferencesStyleCopAndNullableExtendedAnalyzer()
    {
        using var home = new TempHome();
        var project = Evaluate(home, OrdinaryProject);
        await Assert.That(project.GetPropertyValue(UseStyleCopAnalyzers)).IsEqualTo("true");
        await Assert.That(project.GetPropertyValue(UsePublicApiAnalyzers)).IsEqualTo("false");
        await Assert.That(project.GetPropertyValue(UseNullableExtendedAnalyzer)).IsEqualTo("true");
        await Assert.That(GetReferences(project)).IsEquivalentTo([StyleCopAnalyzers, NullableExtendedAnalyzer]);
    }

    [Test]
    public async Task Evaluate_NoTargetsProject_TurnsEveryAnalyzerOff()
    {
        using var home = new TempHome();
        var project = Evaluate(home, NoTargetsProject);
        await Assert.That(project.GetPropertyValue(EnableNETAnalyzers)).IsEqualTo("false");
        await Assert.That(project.GetPropertyValue(UseStyleCopAnalyzers)).IsEqualTo("false");
        await Assert.That(project.GetPropertyValue(UsePublicApiAnalyzers)).IsEqualTo("false");
        await Assert.That(project.GetPropertyValue(UseNullableExtendedAnalyzer)).IsEqualTo("false");
        await Assert.That(GetReferences(project)).IsEmpty();
    }

    [Test]
    public async Task Evaluate_LibraryProject_ReferencesEveryAnalyzerPackage()
    {
        using var home = new TempHome();
        var project = Evaluate(home, LibraryProject);
        await Assert.That(project.GetPropertyValue(UsePublicApiAnalyzers)).IsEqualTo("true");
        string[] expected = [StyleCopAnalyzers, PublicApiAnalyzers, NullableExtendedAnalyzer];
        await Assert.That(GetReferences(project)).IsEquivalentTo(expected);
    }

    // A no-target project that is not a library project defaults every switch to false, so a value that stores 'true'
    // is the one set.
    [Test]
    [MatrixDataSource]
    public async Task Evaluate_SwitchMSBuildReadsAsTrue_StoresTrue(
        [Matrix(EnableNETAnalyzers, UseStyleCopAnalyzers, UsePublicApiAnalyzers, UseNullableExtendedAnalyzer)]
        string name,
        [Matrix("true", "yes", "On", "!false")] string value)
    {
        using var home = new TempHome();
        var project = Evaluate(home, NoTargetsProject + $"<{name}>{value}</{name}>");
        await Assert.That(project.GetPropertyValue(name)).IsEqualTo("true");
    }

    // A library project defaults every switch to true, so a value that stores 'false' is the one set.
    [Test]
    [MatrixDataSource]
    public async Task Evaluate_SwitchOtherThanTrue_StoresFalse(
        [Matrix(EnableNETAnalyzers, UseStyleCopAnalyzers, UsePublicApiAnalyzers, UseNullableExtendedAnalyzer)]
        string name,
        [Matrix("false", "no", "maybe")] string value)
    {
        using var home = new TempHome();
        var project = Evaluate(home, LibraryProject + $"<{name}>{value}</{name}>");
        await Assert.That(project.GetPropertyValue(name)).IsEqualTo("false");
    }

    [Test]
    [Arguments(UseStyleCopAnalyzers, StyleCopAnalyzers)]
    [Arguments(UsePublicApiAnalyzers, PublicApiAnalyzers)]
    [Arguments(UseNullableExtendedAnalyzer, NullableExtendedAnalyzer)]
    public async Task Evaluate_SwitchTrue_ReferencesThePackageWithPrivateAssets(string name, string package)
    {
        using var home = new TempHome();
        var project = Evaluate(home, NoTargetsProject + $"<{name}>true</{name}>");
        await Assert.That(GetReferences(project)).IsEquivalentTo([package]);
        var reference = project.GetItems("BV_PackageReference").Single();
        await Assert.That(reference.GetMetadataValue("PrivateAssets")).IsEqualTo("All");
    }

    [Test]
    [Arguments(UseStyleCopAnalyzers, StyleCopAnalyzers)]
    [Arguments(UsePublicApiAnalyzers, PublicApiAnalyzers)]
    [Arguments(UseNullableExtendedAnalyzer, NullableExtendedAnalyzer)]
    public async Task Evaluate_SwitchFalse_LeavesThePackageOut(string name, string package)
    {
        using var home = new TempHome();
        var project = Evaluate(home, LibraryProject + $"<{name}>false</{name}>");
        await Assert.That(GetReferences(project)).DoesNotContain(package);
    }

    [Test]
    public async Task Evaluate_StyleCopJsonInTheHomeDirectory_AddsItAsAdditionalFile()
    {
        using var home = new TempHome();
        home.WriteFile("stylecop.json", "{}");
        var project = Evaluate(home, OrdinaryProject);
        await Assert.That(GetAdditionalFiles(project)).IsEquivalentTo([(home.GetFullPath("stylecop.json"), "stylecop.json")]);
    }

    [Test]
    public async Task Evaluate_StyleCopJsonNearerTheProject_WinsOverTheOneInTheHomeDirectory()
    {
        using var home = new TempHome();
        home.WriteFile("stylecop.json", "{}");
        home.WriteFile($"{ProjectDirectory}/stylecop.json", "{}");
        var project = Evaluate(home, OrdinaryProject);
        var expected = home.GetFullPath($"{ProjectDirectory}/stylecop.json");
        await Assert.That(GetAdditionalFiles(project)).IsEquivalentTo([(expected, "stylecop.json")]);
    }

    [Test]
    public async Task Evaluate_DotStyleCopJsonAlone_AddsItAsStyleCopJson()
    {
        using var home = new TempHome();
        home.WriteFile("src/.stylecop.json", "{}");
        var project = Evaluate(home, OrdinaryProject);
        await Assert.That(GetAdditionalFiles(project)).IsEquivalentTo([(home.GetFullPath("src/.stylecop.json"), "stylecop.json")]);
    }

    // The search for .stylecop.json starts only when no stylecop.json exists, however far up.
    [Test]
    public async Task Evaluate_StyleCopJsonAndNearerDotStyleCopJson_AddsStyleCopJson()
    {
        using var home = new TempHome();
        home.WriteFile("stylecop.json", "{}");
        home.WriteFile($"{ProjectDirectory}/.stylecop.json", "{}");
        var project = Evaluate(home, OrdinaryProject);
        await Assert.That(GetAdditionalFiles(project)).IsEquivalentTo([(home.GetFullPath("stylecop.json"), "stylecop.json")]);
    }

    // The home directory is the src subdirectory of the temporary directory, and the file is above it.
    [Test]
    [Arguments("stylecop.json")]
    [Arguments(".stylecop.json")]
    public async Task Evaluate_FileAboveTheHomeDirectory_AddsNothing(string fileName)
    {
        using var home = new TempHome();
        home.WriteFile(fileName, "{}");
        var properties = OrdinaryProject + $"<HomeDirectory>{home.GetFullPath("src")}{Path.DirectorySeparatorChar}</HomeDirectory>";
        var project = Evaluate(home, properties);
        await Assert.That(project.GetPropertyValue("StyleCopJsonPath")).IsEmpty();
        await Assert.That(GetAdditionalFiles(project)).IsEmpty();
    }

    [Test]
    public async Task Evaluate_StyleCopJsonListedByTheProject_AddsItOnceAsAdditionalFile()
    {
        using var home = new TempHome();
        home.WriteFile($"{ProjectDirectory}/stylecop.json", "{}");
        const string items = """
            <None Include="stylecop.json" />
            <AdditionalFiles Include="stylecop.json" />
            """;
        var project = Evaluate(home, OrdinaryProject, items);
        var expected = home.GetFullPath($"{ProjectDirectory}/stylecop.json");
        await Assert.That(GetAdditionalFiles(project)).IsEquivalentTo([(expected, "stylecop.json")]);
        await Assert.That(project.GetItems("None")).IsEmpty();
    }

    [Test]
    public async Task Evaluate_UseStyleCopAnalyzersFalse_AddsNoStyleCopJson()
    {
        using var home = new TempHome();
        home.WriteFile("stylecop.json", "{}");
        var project = Evaluate(home, OrdinaryProject + "<UseStyleCopAnalyzers>false</UseStyleCopAnalyzers>");
        await Assert.That(GetAdditionalFiles(project)).IsEmpty();
    }

    // The project is in src/Test, and the home directory is the temporary directory, unless the properties set
    // HomeDirectory.
    private static ProjectInstance Evaluate(TempHome home, string properties, string items = "")
    {
        var projectPath = home.GetFullPath($"{ProjectDirectory}/Test.proj");
        var projectText = $"""
            <Project>
              <PropertyGroup>
                <HomeDirectory>{home.RootPath}{Path.DirectorySeparatorChar}</HomeDirectory>
                {properties}
              </PropertyGroup>
              <ItemGroup>
                {items}
              </ItemGroup>
              <Import Project="{GetRealTargetsPath()}" />
            </Project>
            """;
        home.WriteFile($"{ProjectDirectory}/Test.proj", projectText);

        using var collection = new ProjectCollection();
        return ProjectInstance.FromFile(projectPath, new ProjectOptions { ProjectCollection = collection });
    }

    private static string[] GetReferences(ProjectInstance project)
        => [.. project.GetItems("BV_PackageReference").Select(static item => item.EvaluatedInclude)];

    // Each additional file as its full path and its Link metadata.
    private static (string Path, string Link)[] GetAdditionalFiles(ProjectInstance project)
        => [
            .. project.GetItems("AdditionalFiles")
                .Select(static item => (item.GetMetadataValue("FullPath"), item.GetMetadataValue("Link"))),
        ];

    private static string GetRealTargetsPath()
        => typeof(StandardAnalyzersModuleTargetsTests).Assembly.GetMetadata("RealStandardAnalyzersModuleTargetsPath");
}
