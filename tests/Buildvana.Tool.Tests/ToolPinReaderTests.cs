// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.Json;
using Buildvana.Core.Testing;
using Buildvana.Tool.Services.Dependencies;

internal sealed class ToolPinReaderTests
{
    [Test]
    public async Task Read_WithNoManifest_PinsNothing()
    {
        using var home = new TempHome();
        await Assert.That(CreateReader(home).Read()).IsEmpty();
    }

    [Test]
    public async Task Read_StatesEveryToolTheManifestPins()
    {
        const string content = """
                               {
                                 "version": 1,
                                 "isRoot": true,
                                 "tools": {
                                   "dotnet-format": { "version": "5.1.250801", "commands": ["dotnet-format"] },
                                   "ngbv": { "version": "0.5.1", "commands": ["ngbv"] }
                                 }
                               }
                               """;
        using var home = new TempHome();
        Write(home, content);
        var pins = CreateReader(home).Read();
        await Assert.That(pins.Select(static pin => pin.Id + " " + pin.VersionText))
            .IsEquivalentTo(["dotnet-format 5.1.250801", "ngbv 0.5.1"]);
        await Assert.That(pins[0].Scope).IsEqualTo(DependencyScope.Tools);
        await Assert.That(pins[0].DeclaringFile).IsEqualTo("dotnet-tools.json");
    }

    // A manifest in a subdirectory is a file the repository owns, and its pins belong to it, the way the
    // packages scope reads every project file. The repository walk yields docs/ before the root file.
    [Test]
    public async Task Read_StatesThePinsOfEveryManifestUnderTheHomeDirectory()
    {
        using var home = new TempHome();
        home.WriteFile("dotnet-tools.json", """{ "tools": { "ngbv": { "version": "0.5.1" } } }""");
        home.WriteFile("docs/dotnet-tools.json", """{ "tools": { "docfx": { "version": "2.78.3" } } }""");
        var pins = CreateReader(home).Read();
        await Assert.That(pins.Select(static pin => pin.Id + " " + pin.DeclaringFile))
            .IsEquivalentTo(["docfx docs/dotnet-tools.json", "ngbv dotnet-tools.json"]);
    }

    // Build debris and ignored directories are not the repository's: a manifest there is nobody's pin.
    [Test]
    public async Task Read_LeavesIgnoredDirectoriesAlone()
    {
        using var home = new TempHome();
        home.WriteFile(".gitignore", "/scratch/\n");
        home.WriteFile("bin/dotnet-tools.json", """{ "tools": { "ngbv": { "version": "0.5.1" } } }""");
        home.WriteFile("scratch/dotnet-tools.json", """{ "tools": { "ngbv": { "version": "0.5.1" } } }""");
        await Assert.That(CreateReader(home).Read()).IsEmpty();
    }

    // The .NET SDK before version 10 created the manifest under .config, and the dotnet CLI still reads it
    // there. bv does not, and says so instead of managing a file the CLI would write into.
    [Test]
    public async Task Read_WithAManifestUnderDotConfig_NamesEachOneAndItsMove()
    {
        using var home = new TempHome();
        home.WriteFile(".config/dotnet-tools.json", """{ "tools": { } }""");
        home.WriteFile("docs/.config/dotnet-tools.json", """{ "tools": { } }""");

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        var exception = await Assert.That(() => CreateReader(home).Read()).Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains("git mv .config/dotnet-tools.json dotnet-tools.json");
        await Assert.That(exception.Message).Contains("git mv docs/.config/dotnet-tools.json docs/dotnet-tools.json");
    }

    // bv moves with the family, so bv dependencies never sees its own entry.
    [Test]
    public async Task Read_LeavesTheFamilyToolOut()
    {
        const string content = """
                               {
                                 "tools": {
                                   "bv": { "version": "2.1.367-preview", "commands": ["bv"] },
                                   "ngbv": { "version": "0.5.1", "commands": ["ngbv"] }
                                 }
                               }
                               """;
        using var home = new TempHome();
        Write(home, content);
        await Assert.That(CreateReader(home).Read().Single().Id).IsEqualTo("ngbv");
    }

    [Test]
    [Arguments("{ }")]
    [Arguments("""{ "tools": { } }""")]
    [Arguments("""{ "tools": { "ngbv": { "commands": ["ngbv"] } } }""")]
    public async Task Read_WithNoUsableEntry_PinsNothing(string content)
    {
        using var home = new TempHome();
        Write(home, content);
        await Assert.That(CreateReader(home).Read()).IsEmpty();
    }

    private static ToolPinReader CreateReader(TempHome home) => new(home.Provider, new JsonHelper());

    private static void Write(TempHome home, string content) => home.WriteFile("dotnet-tools.json", content);
}
