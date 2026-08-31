// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Testing;
using Buildvana.Tool.Services.Dependencies;
using NuGet.Versioning;

// The source under test talks to a folder feed, which NuGet reads through the same client libraries it reads
// a server with. What a folder cannot express — a delisted version, a source that refuses to answer — is not
// tested here: the catalog's own tests cover the shape of the answer.
internal sealed class NuGetPackageVersionSourceTests
{
    private const string ConfigFileName = "nuget.config";

    private const string Config = """
                                  <?xml version="1.0" encoding="utf-8"?>
                                  <configuration>
                                    <packageSources>
                                      <clear />
                                      <add key="local" value="feed" />
                                    </packageSources>
                                  </configuration>
                                  """;

    [Test]
    public async Task Sources_NameTheConfiguredSources()
    {
        using var home = NewHome();
        using var source = NewSource(home);
        await Assert.That(source.Sources).IsEquivalentTo(["local"]);
    }

    [Test]
    public async Task GetVersionsAsync_ListsWhatTheFeedHas()
    {
        using var home = NewHome();
        WritePackages(home, "1.0.0", "1.1.0-preview.1", "1.1.0");
        using var source = NewSource(home);
        var catalog = await source.GetVersionsAsync("Contoso.Widgets").ConfigureAwait(false);
        await Assert.That(catalog.Listed.Select(static version => version.ToNormalizedString()))
            .IsEquivalentTo(["1.0.0", "1.1.0-preview.1", "1.1.0"]);
        await Assert.That(catalog.Unlisted).IsEmpty();
    }

    [Test]
    public async Task GetVersionsAsync_MatchesAnIdWhateverItsCase()
    {
        using var home = NewHome();
        WritePackages(home, "1.0.0");
        using var source = NewSource(home);
        var catalog = await source.GetVersionsAsync("contoso.widgets").ConfigureAwait(false);
        await Assert.That(catalog.IsListed(NuGetVersion.Parse("1.0.0"))).IsTrue();
    }

    [Test]
    public async Task GetVersionsAsync_ForAnIdNoSourceHas_KnowsNothing()
    {
        using var home = NewHome();
        WritePackages(home, "1.0.0");
        using var source = NewSource(home);
        var catalog = await source.GetVersionsAsync("Northwind.Data").ConfigureAwait(false);
        await Assert.That(catalog.Listed).IsEmpty();
        await Assert.That(catalog.Unlisted).IsEmpty();
    }

    private static TempHome NewHome()
    {
        var home = new TempHome();
        home.WriteFile(ConfigFileName, Config);
        return home;
    }

    private static void WritePackages(TempHome home, params string[] versions)
    {
        foreach (var version in versions)
        {
            LocalPackageFeed.WritePackage(home.GetFullPath("feed"), "Contoso.Widgets", version);
        }
    }

    private static NuGetPackageVersionSource NewSource(TempHome home)
        => new(new PackageSourceCatalog(home.Provider), NullReporter.Instance);
}
