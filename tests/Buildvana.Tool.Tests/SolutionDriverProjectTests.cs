// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Services.Solution;

internal sealed class SolutionDriverProjectTests
{
    private const string ProjectTargetName = "BV_DumpPackagePins";

    [Test]
    public async Task Create_StatesOneItemPerProject()
    {
        var project = SolutionDriverProject.Create([@"C:\repo\src\A\A.csproj", @"C:\repo\src\B\B.csproj"], ProjectTargetName);
        await Assert.That(project).Contains("""<BV_SolutionProject Include="C:\repo\src\A\A.csproj" />""");
        await Assert.That(project).Contains("""<BV_SolutionProject Include="C:\repo\src\B\B.csproj" />""");
    }

    [Test]
    public async Task Create_AsksEachProjectForTheTarget()
    {
        var project = SolutionDriverProject.Create([@"C:\repo\src\A\A.csproj"], ProjectTargetName);
        await Assert.That(project).Contains($"""<Target Name="{SolutionDriverProject.TargetName}">""");
        await Assert.That(project).Contains(""""Targets="BV_DumpPackagePins"""");
    }

    // A project the SDK never reaches has no such target, and must not end the run.
    [Test]
    public async Task Create_SkipsAProjectWithoutTheTarget()
    {
        var project = SolutionDriverProject.Create([@"C:\repo\src\A\A.csproj"], ProjectTargetName);
        await Assert.That(project).Contains(""""SkipNonexistentTargets="true"""");
    }

    [Test]
    [Arguments(@"C:\repo\a;b\A.csproj", "a%3Bb")]
    [Arguments(@"C:\Program Files (x86)\A.csproj", "Program Files %28x86%29")]
    [Arguments(@"C:\repo\100%\A.csproj", "100%25")]
    [Arguments(@"C:\repo\$(x)\A.csproj", "%24%28x%29")]
    [Arguments(@"C:\repo\@list\A.csproj", "%40list")]
    [Arguments(@"C:\repo\a*b\A.csproj", "a%2Ab")]
    public async Task Create_EscapesWhatMsBuildWouldReadAsSyntax(string path, string expected)
    {
        await Assert.That(SolutionDriverProject.Create([path], ProjectTargetName)).Contains(expected);
    }

    [Test]
    public async Task Create_EscapesWhatXmlWouldReadAsMarkup()
    {
        var project = SolutionDriverProject.Create([@"C:\repo\a&b\A.csproj"], ProjectTargetName);
        await Assert.That(project).Contains("a&amp;b");
    }
}
