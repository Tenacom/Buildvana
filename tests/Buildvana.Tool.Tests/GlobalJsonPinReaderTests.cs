// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Json;
using Buildvana.Core.Testing;
using Buildvana.Tool.Services.Dependencies;

internal sealed class GlobalJsonPinReaderTests
{
    [Test]
    public async Task Read_WithNoFile_PinsNothing()
    {
        using var home = new TempHome();
        var pins = CreateReader(home).Read();
        await Assert.That(pins.NetSdk).IsNull();
        await Assert.That(pins.Sdks).IsEmpty();
    }

    [Test]
    public async Task Read_StatesTheNetSdkBaselineAndItsPrereleaseSetting()
    {
        const string content = """
                               {
                                 "sdk": { "version": "10.0.100", "allowPrerelease": true }
                               }
                               """;
        using var home = new TempHome();
        Write(home, content);
        var netSdk = CreateReader(home).Read().NetSdk!;
        await Assert.That(netSdk.VersionText).IsEqualTo("10.0.100");
        await Assert.That(netSdk.Version?.ToNormalizedString()).IsEqualTo("10.0.100");
        await Assert.That(netSdk.AllowPrerelease).IsTrue();
    }

    // The setting is derived state, and its absence is a state of its own: the scope's policy decides what
    // it should say, and a later step compares the two.
    [Test]
    public async Task Read_WithNoPrereleaseSetting_LeavesItUnstated()
    {
        using var home = new TempHome();
        Write(home, """{ "sdk": { "version": "10.0.100" } }""");
        await Assert.That(CreateReader(home).Read().NetSdk!.AllowPrerelease).IsNull();
    }

    [Test]
    [Arguments("{ }")]
    [Arguments("""{ "sdk": { } }""")]
    [Arguments("""{ "sdk": { "rollForward": "latestFeature" } }""")]
    public async Task Read_WithNoBaseline_PinsNoNetSdk(string content)
    {
        using var home = new TempHome();
        Write(home, content);
        await Assert.That(CreateReader(home).Read().NetSdk).IsNull();
    }

    [Test]
    public async Task Read_StatesTheProjectSdksInFileOrder()
    {
        const string content = """
                               {
                                 "msbuild-sdks": {
                                   "Microsoft.Build.NoTargets": "3.7.0",
                                   "Microsoft.Build.Traversal": "4.1.0"
                                 }
                               }
                               """;
        using var home = new TempHome();
        Write(home, content);
        var sdks = CreateReader(home).Read().Sdks;
        await Assert.That(sdks.Select(static pin => pin.Id + " " + pin.VersionText))
            .IsEquivalentTo(["Microsoft.Build.NoTargets 3.7.0", "Microsoft.Build.Traversal 4.1.0"]);
        await Assert.That(sdks[0].Scope).IsEqualTo(DependencyScope.Sdks);
        await Assert.That(sdks[0].DeclaringFile).IsEqualTo("global.json");
    }

    // The Buildvana SDK moves with the family, so bv dependencies never sees it.
    [Test]
    public async Task Read_LeavesTheFamilySdkOut()
    {
        const string content = """
                               {
                                 "msbuild-sdks": {
                                   "Buildvana.Sdk": "2.1.0",
                                   "Microsoft.Build.NoTargets": "3.7.0"
                                 }
                               }
                               """;
        using var home = new TempHome();
        Write(home, content);
        await Assert.That(CreateReader(home).Read().Sdks.Single().Id).IsEqualTo("Microsoft.Build.NoTargets");
    }

    private static GlobalJsonPinReader CreateReader(TempHome home) => new(home.Provider, new JsonHelper());

    private static void Write(TempHome home, string content)
        => File.WriteAllText(home.GetFullPath("global.json"), content);
}
