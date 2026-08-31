// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

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
        await Assert.That(pins[0].DeclaringFile).IsEqualTo(".config/dotnet-tools.json");
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

    private static void Write(TempHome home, string content)
    {
        var path = home.GetFullPath(".config/dotnet-tools.json");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
