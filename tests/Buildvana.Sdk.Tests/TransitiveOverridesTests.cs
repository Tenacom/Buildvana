// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

internal sealed class TransitiveOverridesTests
{
    private const string CentralFile = """
                                       <Project>
                                         <ItemGroup>
                                           <PackageVersion Include="System.Text.Json" Version="10.0.1" />
                                         </ItemGroup>
                                       </Project>
                                       """;

    private const string ProjectFile = """
                                       <Project>
                                         <ItemGroup>
                                           <PackageReference Include="System.Text.Json" PrivateAssets="all" />
                                         </ItemGroup>
                                       </Project>
                                       """;

    [Test]
    public async Task Evaluation_WithNoOverrideFile_HasNoOverrides()
    {
        using var fixture = new TransitiveOverridesFixture();
        await Assert.That(fixture.EvaluateItems("PackageVersion")).IsEmpty();
        await Assert.That(fixture.EvaluateItems("PackageReference")).IsEmpty();
    }

    [Test]
    public async Task Evaluation_ImportsTheCentralOverrideFile()
    {
        using var fixture = new TransitiveOverridesFixture();
        fixture.WriteHomeFile("Directory.TransitiveOverrides.props", CentralFile);
        await Assert.That(fixture.EvaluateItems("PackageVersion").Single()).IsEqualTo("System.Text.Json");
    }

    [Test]
    public async Task Evaluation_ImportsTheProjectOverrideFile()
    {
        using var fixture = new TransitiveOverridesFixture();
        fixture.WriteProjectFile("Test.TransitiveOverrides.props", ProjectFile);
        await Assert.That(fixture.EvaluateItems("PackageReference").Single()).IsEqualTo("System.Text.Json");
    }

    [Test]
    public async Task Evaluation_UnderSuppression_ImportsNeitherOverrideFile()
    {
        using var fixture = new TransitiveOverridesFixture();
        fixture.WriteHomeFile("Directory.TransitiveOverrides.props", CentralFile);
        fixture.WriteProjectFile("Test.TransitiveOverrides.props", ProjectFile);
        await Assert.That(fixture.EvaluateItems("PackageVersion", suppressOverrides: true)).IsEmpty();
        await Assert.That(fixture.EvaluateItems("PackageReference", suppressOverrides: true)).IsEmpty();
    }

    // Nothing but the project's own file is imported: a sibling project's overrides are its own, and a
    // project that needs none has no file to import.
    [Test]
    public async Task Evaluation_IgnoresAnotherProjectsOverrideFile()
    {
        using var fixture = new TransitiveOverridesFixture();
        fixture.WriteProjectFile("Other.TransitiveOverrides.props", ProjectFile);
        await Assert.That(fixture.EvaluateItems("PackageReference")).IsEmpty();
    }
}
