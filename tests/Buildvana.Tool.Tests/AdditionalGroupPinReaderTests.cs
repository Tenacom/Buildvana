// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Process;
using Buildvana.Core.Testing;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Dependencies;

// The reader evaluates each of a group's files with `dotnet msbuild -getItem`, so the process runner is a
// fake here, scripted to answer with the JSON MSBuild would have written.
internal sealed class AdditionalGroupPinReaderTests
{
    private const string GroupFile = "src/Sdk/PackageVersions.props";

    private const string GroupFileContent = """
                                            <Project>
                                              <ItemGroup>
                                                <BV_PackageVersion Include="StyleCop.Analyzers" Version="1.2.0-beta.556" UpdatePolicy="patch-" />
                                                <BV_PackageVersion Include="Tools.InnoSetup" Version="7.1.0" />
                                              </ItemGroup>
                                            </Project>
                                            """;

    [Test]
    public async Task ReadAsync_StatesThePinsOfEveryGroupFile()
    {
        using var home = new TempHome();
        Write(home, GroupFile, GroupFileContent);
        var runner = Answer(home, ("StyleCop.Analyzers", "1.2.0-beta.556", "patch-"), ("Tools.InnoSetup", "7.1.0", null));
        var pins = await ReadAsync(home, runner).ConfigureAwait(false);
        await Assert.That(pins.Select(static pin => pin.Id + " " + pin.VersionText))
            .IsEquivalentTo(["StyleCop.Analyzers 1.2.0-beta.556", "Tools.InnoSetup 7.1.0"]);
        await Assert.That(pins[0].GroupCaption).IsEqualTo("SDK package injections");
        await Assert.That(pins[0].ItemType).IsEqualTo("BV_PackageVersion");
        await Assert.That(pins[0].MetadataPolicy).IsEqualTo("patch-");
        await Assert.That(pins[0].DeclaringFile).IsEqualTo(GroupFile);
        await Assert.That(pins[0].Management).IsEqualTo(PinManagement.Managed);
    }

    [Test]
    public async Task ReadAsync_AsksMsBuildForTheGroupsItemName()
    {
        using var home = new TempHome();
        Write(home, GroupFile, GroupFileContent);
        var runner = Answer(home, ("Tools.InnoSetup", "7.1.0", null));
        _ = await ReadAsync(home, runner).ConfigureAwait(false);
        var args = string.Join(" ", runner.Runs.Single().Args);
        await Assert.That(args).Contains("msbuild");
        await Assert.That(args).Contains("-getItem:BV_PackageVersion");
        await Assert.That(args).Contains("PackageVersions.props");
    }

    [Test]
    public async Task ReadAsync_WithNoGroup_EvaluatesNothing()
    {
        using var home = new TempHome();
        var runner = new FakeProcessRunner();
        var pins = await new AdditionalGroupPinReader(home.Provider, new BuildvanaConfig(), runner, NullReporter.Instance)
            .ReadAsync()
            .ConfigureAwait(false);
        await Assert.That(pins).IsEmpty();
        await Assert.That(runner.Runs).IsEmpty();
    }

    // An item an import brought in from outside the group's glob belongs to whatever declares it.
    [Test]
    public async Task ReadAsync_LeavesOutAnItemDeclaredOutsideTheGlob()
    {
        using var home = new TempHome();
        Write(home, GroupFile, GroupFileContent);
        var elsewhere = home.GetFullPath("src/Other/Imported.props");
        var runner = Answer([("Tools.InnoSetup", "7.1.0", null)], declaringFile: elsewhere);
        await Assert.That(await ReadAsync(home, runner).ConfigureAwait(false)).IsEmpty();
    }

    [Test]
    public async Task ReadAsync_LeavesTheFamilyOut()
    {
        using var home = new TempHome();
        Write(home, GroupFile, GroupFileContent);
        var runner = Answer(home, ("Buildvana.Runtime", "2.1.0", null));
        await Assert.That(await ReadAsync(home, runner).ConfigureAwait(false)).IsEmpty();
    }

    // The file states the version through a property, so what evaluation produced is not what an update
    // could write back.
    [Test]
    public async Task ReadAsync_OfAVersionStatedThroughAProperty_IsIndirect()
    {
        const string content = """
                               <Project>
                                 <PropertyGroup>
                                   <InnoSetupVersion>7.1.0</InnoSetupVersion>
                                 </PropertyGroup>
                                 <ItemGroup>
                                   <BV_PackageVersion Include="Tools.InnoSetup" Version="$(InnoSetupVersion)" />
                                 </ItemGroup>
                               </Project>
                               """;

        using var home = new TempHome();
        Write(home, GroupFile, content);
        var runner = Answer(home, ("Tools.InnoSetup", "7.1.0", null));
        var pins = await ReadAsync(home, runner).ConfigureAwait(false);
        await Assert.That(pins.Single().Management).IsEqualTo(PinManagement.IndirectVersion);
    }

    // MSBuild carries the layout of a Version child element into the value it evaluates, and the group's
    // file states that same layout: the pin is as managed as one whose version is an attribute. The `\n` of
    // the evaluated version are JSON escapes, since the value below is written into MSBuild's JSON answer.
    [Test]
    public async Task ReadAsync_OfAVersionStatedAsAChildElement_IsManaged()
    {
        const string content = """
                               <Project>
                                 <ItemGroup>
                                   <BV_PackageVersion Include="Tools.InnoSetup">
                                     <Version>
                                       7.1.0
                                     </Version>
                                   </BV_PackageVersion>
                                 </ItemGroup>
                               </Project>
                               """;

        using var home = new TempHome();
        Write(home, GroupFile, content);
        var runner = Answer(home, ("Tools.InnoSetup", @"\n      7.1.0\n    ", null));
        var pin = (await ReadAsync(home, runner).ConfigureAwait(false)).Single();
        await Assert.That(pin.Management).IsEqualTo(PinManagement.Managed);
        await Assert.That(pin.VersionText).IsEqualTo("7.1.0");
    }

    [Test]
    public async Task ReadAsync_WhenEvaluationFails_ReportsAFailedStep()
    {
        using var home = new TempHome();
        Write(home, GroupFile, GroupFileContent);
        var runner = new FakeProcessRunner
        {
            OnRun = static (executable, _)
                => new ProcessResult(executable, 1, "error MSB4025: no.", string.Empty, TimeSpan.Zero),
        };

        var reader = CreateReader(home, runner);
        var exception = await Assert.That(async () => await reader.ReadAsync().ConfigureAwait(false))
            .Throws<BuildFailedException>();
        await Assert.That(exception!.ExitCode).IsEqualTo(3);
    }

    private static Task<IReadOnlyList<DependencyPin>> ReadAsync(TempHome home, IProcessRunner runner)
        => CreateReader(home, runner).ReadAsync();

    private static AdditionalGroupPinReader CreateReader(TempHome home, IProcessRunner runner)
    {
        var config = new BuildvanaConfig
        {
            Dependencies = new DependenciesConfig
            {
                AdditionalPackages =
                [
                    new AdditionalPackagesConfig
                    {
                        Caption = "SDK package injections",
                        Files = "src/Sdk/PackageVersions.props",
                        Items = "BV_PackageVersion",
                        Policy = "minor",
                    },
                ],
            },
        };

        return new AdditionalGroupPinReader(home.Provider, config, runner, NullReporter.Instance);
    }

    private static FakeProcessRunner Answer(TempHome home, params (string Id, string Version, string? Policy)[] items)
        => Answer(items, home.GetFullPath(GroupFile));

    private static FakeProcessRunner Answer(
        IReadOnlyList<(string Id, string Version, string? Policy)> items,
        string declaringFile)
    {
        var elements = items.Select(item => $$"""
                                              {
                                                "Identity": "{{item.Id}}",
                                                "Version": "{{item.Version}}",
                                                "UpdatePolicy": "{{item.Policy}}",
                                                "DefiningProjectFullPath": {{JsonPath(declaringFile)}}
                                              }
                                              """);

        var output = $$"""
                       { "Items": { "BV_PackageVersion": [ {{string.Join(",", elements)}} ] } }
                       """;

        return new FakeProcessRunner
        {
            OnRun = (executable, _) => new ProcessResult(executable, 0, output, string.Empty, TimeSpan.Zero),
        };
    }

    private static string JsonPath(string path) => "\"" + path.Replace(@"\", @"\\", StringComparison.Ordinal) + "\"";

    private static void Write(TempHome home, string relativePath, string content)
    {
        var path = home.GetFullPath(relativePath);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
