// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Json;
using Buildvana.Core.Testing;
using Buildvana.Tool.Services;

internal sealed class SelfReferenceUpdaterTests
{
    private const string Manifest
        = """{ "version": 1, "isRoot": true, "tools": { "bv": { "version": "2.1.40-preview", "commands": [ "bv" ] } } }""";

    [Test]
    public async Task UpdateReferences_RewritesTheToolManifest()
    {
        using var home = new TempHome();
        home.WriteFile("dotnet-tools.json", Manifest);
        var modified = CreateUpdater(home).UpdateReferences(new Dictionary<string, string> { ["bv"] = "2.1.41-preview" });
        await Assert.That(modified.Count).IsEqualTo(1);
        await Assert.That(home.ReadFile("dotnet-tools.json")).Contains("2.1.41-preview");
    }

    // The rewrite reads the manifest where bv reads it. A manifest under .config would be skipped, and the
    // post-release commit would leave bv's own pin behind.
    [Test]
    public async Task UpdateReferences_WithManifestUnderDotConfig_Fails()
    {
        using var home = new TempHome();
        home.WriteFile(".config/dotnet-tools.json", Manifest);
        var updater = CreateUpdater(home);

        var exception = await Assert
            .That(() => updater.UpdateReferences(new Dictionary<string, string> { ["bv"] = "2.1.41-preview" }))
            .Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains("git mv .config/dotnet-tools.json dotnet-tools.json");
        await Assert.That(home.ReadFile(".config/dotnet-tools.json")).IsEqualTo(Manifest);
    }

    private static SelfReferenceUpdater CreateUpdater(TempHome home) => new(NullReporter.Instance, home.Provider, new JsonHelper());
}
