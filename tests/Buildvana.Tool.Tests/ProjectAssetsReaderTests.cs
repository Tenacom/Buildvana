// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.Testing;
using Buildvana.Tool.Services.Dependencies;
using NuGet.Common;

internal sealed class ProjectAssetsReaderTests
{
    private const string ProjectFileName = "Test.csproj";
    private const string AssetsFileName = "obj/project.assets.json";

    // A restore's own output, cut down to the sections bv reads. The second log entry names its target graph
    // in the long form, which is the form older assets files use for every one of them.
    // Do not trim it further: NuGet's reader resolves a framework of the project through the alias its
    // restore metadata states, and a file without those two sections fails to parse as a whole.
    private const string AssetsContent = """
                                         {
                                           "version": 4,
                                           "targets": {
                                             "net10.0": {
                                               "Serilog/4.0.0": { "type": "package" },
                                               "Newtonsoft.Json/12.0.2": { "type": "package" },
                                               "Buildvana.Core/1.0.0": { "type": "project" }
                                             }
                                           },
                                           "libraries": {},
                                           "projectFileDependencyGroups": { "net10.0": [ "Serilog >= 4.0.0" ] },
                                           "packageFolders": {},
                                           "project": {
                                             "version": "1.0.0",
                                             "restore": {
                                               "projectUniqueName": "Test.csproj",
                                               "projectName": "Test",
                                               "projectPath": "Test.csproj",
                                               "projectStyle": "PackageReference",
                                               "originalTargetFrameworks": [ "net10.0" ],
                                               "frameworks": {
                                                 "net10.0": { "targetAlias": "net10.0" }
                                               }
                                             },
                                             "frameworks": {
                                               "net10.0": {
                                                 "targetAlias": "net10.0",
                                                 "dependencies": {
                                                   "Serilog": { "target": "Package", "version": "[4.0.0, )" }
                                                 }
                                               }
                                             }
                                           },
                                           "logs": [
                                             {
                                               "code": "NU1902",
                                               "level": "Warning",
                                               "warningLevel": 1,
                                               "message": "Package 'Newtonsoft.Json' 12.0.2 has a known moderate severity vulnerability",
                                               "libraryId": "Newtonsoft.Json",
                                               "targetGraphs": [ "net10.0" ]
                                             },
                                             {
                                               "code": "NU1903",
                                               "level": "Warning",
                                               "warningLevel": 1,
                                               "message": "Package 'Other' 1.0.0 has a known high severity vulnerability",
                                               "libraryId": "Other",
                                               "targetGraphs": [ ".NETCoreApp,Version=v10.0" ]
                                             }
                                           ]
                                         }
                                         """;

    [Test]
    public async Task Read_StatesThePackagesTheRestoreResolved()
    {
        using var home = new TempHome();
        var assets = Read(home);
        var packages = assets.Packages.Select(static package => package.Id + " " + package.Version.ToNormalizedString()).Order(StringComparer.Ordinal);
        await Assert.That(string.Join(", ", packages)).IsEqualTo("Newtonsoft.Json 12.0.2, Serilog 4.0.0");
    }

    [Test]
    public async Task Read_StatesWhatTheProjectReferencesItself()
    {
        using var home = new TempHome();
        var assets = Read(home);
        await Assert.That(assets.DirectReferences).IsEquivalentTo(["Serilog"]);
    }

    [Test]
    public async Task Read_StatesTheRestoreLog()
    {
        using var home = new TempHome();
        var entry = Read(home).Logs[0];
        await Assert.That(entry.Code).IsEqualTo(NuGetLogCode.NU1902);
        await Assert.That(entry.Level).IsEqualTo(LogLevel.Warning);
        await Assert.That(entry.LibraryId).IsEqualTo("Newtonsoft.Json");
        await Assert.That(entry.Message).Contains("moderate severity");
    }

    // The point of naming a target graph in one form: a caller matches a finding against the graph it
    // concerns by comparing the two names.
    [Test]
    public async Task Read_NamesOneTargetGraphOneWay()
    {
        using var home = new TempHome();
        var assets = Read(home);
        var resolved = assets.Packages[0].TargetGraph;
        await Assert.That(assets.Logs[0].TargetGraphs.Single()).IsEqualTo(resolved);
        await Assert.That(assets.Logs[1].TargetGraphs.Single()).IsEqualTo(resolved);
    }

    [Test]
    public async Task Read_WithNoAssetsFile_ReportsAFailedStep()
    {
        using var home = new TempHome();
        var project = home.GetFullPath(ProjectFileName);
        var assetsPath = home.GetFullPath(AssetsFileName);
        var exception = await Assert.That(() => ProjectAssetsReader.Read(project, assetsPath)).Throws<BuildFailedException>();
        await Assert.That(exception!.ExitCode).IsEqualTo(3);
    }

    [Test]
    public async Task Read_WithAGraphThatDoesNotParse_ReportsAFailedStep()
    {
        using var home = new TempHome();
        home.WriteFile(AssetsFileName, "this is not an assets file");
        var project = home.GetFullPath(ProjectFileName);
        var assetsPath = home.GetFullPath(AssetsFileName);
        var exception = await Assert.That(() => ProjectAssetsReader.Read(project, assetsPath)).Throws<BuildFailedException>();
        await Assert.That(exception!.ExitCode).IsEqualTo(3);
    }

    private static ProjectAssets Read(TempHome home)
    {
        home.WriteFile(AssetsFileName, AssetsContent);
        return ProjectAssetsReader.Read(home.GetFullPath(ProjectFileName), home.GetFullPath(AssetsFileName));
    }
}
