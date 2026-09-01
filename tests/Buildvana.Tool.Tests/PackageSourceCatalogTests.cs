// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Testing;
using Buildvana.Tool.Services.Dependencies;

// Every configuration here starts with a <clear />, which is what makes these tests say something: without
// it the machine's own user-level sources would be in the answer too, as they are in a real repository.
internal sealed class PackageSourceCatalogTests
{
    private const string ConfigFileName = "nuget.config";

    private const string TwoSources = """
                                        <packageSources>
                                          <clear />
                                          <add key="local" value="feed" />
                                          <add key="other" value="other-feed" />
                                        </packageSources>
                                      """;

    private const string OtherDisabled = """
                                           <disabledPackageSources>
                                             <add key="other" value="true" />
                                           </disabledPackageSources>
                                         """;

    private const string AuditSource = """
                                         <auditSources>
                                           <clear />
                                           <add key="audit" value="audit-feed" />
                                         </auditSources>
                                       """;

    private const string Mapping = """
                                     <packageSourceMapping>
                                       <packageSource key="local">
                                         <package pattern="Contoso.*" />
                                       </packageSource>
                                       <packageSource key="other">
                                         <package pattern="Fabrikam.*" />
                                       </packageSource>
                                     </packageSourceMapping>
                                   """;

    private const string MappingToTheDisabledSource = """
                                                        <packageSourceMapping>
                                                          <packageSource key="other">
                                                            <package pattern="Contoso.*" />
                                                          </packageSource>
                                                        </packageSourceMapping>
                                                      """;

    [Test]
    public async Task Sources_AreTheEnabledSourcesOfTheChain()
    {
        using var home = NewHome(TwoSources);
        var catalog = new PackageSourceCatalog(home.Provider);
        await Assert.That(catalog.Sources.Select(static source => source.Name)).IsEquivalentTo(["local", "other"]);
    }

    [Test]
    public async Task Sources_LeaveOutADisabledSource()
    {
        using var home = NewHome(TwoSources + "\n" + OtherDisabled);
        var catalog = new PackageSourceCatalog(home.Provider);
        await Assert.That(catalog.Sources.Select(static source => source.Name)).IsEquivalentTo(["local"]);
    }

    [Test]
    public async Task AuditSources_WithNoneConfigured_AreThePackageSources()
    {
        using var home = NewHome(TwoSources);
        var catalog = new PackageSourceCatalog(home.Provider);
        await Assert.That(catalog.AuditSources.Select(static source => source.Name)).IsEquivalentTo(["local", "other"]);
    }

    [Test]
    public async Task AuditSources_WithSomeConfigured_AreThoseAlone()
    {
        using var home = NewHome(TwoSources + "\n" + AuditSource);
        var catalog = new PackageSourceCatalog(home.Provider);
        await Assert.That(catalog.AuditSources.Select(static source => source.Name)).IsEquivalentTo(["audit"]);
    }

    [Test]
    public async Task SourcesFor_WithNoSourceMapping_IsEverySource()
    {
        using var home = NewHome(TwoSources);
        var catalog = new PackageSourceCatalog(home.Provider);
        await Assert.That(catalog.SourcesFor("Contoso.Widgets").Select(static source => source.Name))
            .IsEquivalentTo(["local", "other"]);
    }

    [Test]
    public async Task SourcesFor_WithSourceMapping_IsWhatTheIdMapsTo()
    {
        using var home = NewHome(TwoSources + "\n" + Mapping);
        var catalog = new PackageSourceCatalog(home.Provider);
        await Assert.That(catalog.SourcesFor("Contoso.Widgets").Select(static source => source.Name)).IsEquivalentTo(["local"]);
        await Assert.That(catalog.SourcesFor("Fabrikam.Core").Select(static source => source.Name)).IsEquivalentTo(["other"]);
    }

    [Test]
    public async Task SourcesFor_WhenNoPatternMatchesTheId_IsEmpty()
    {
        using var home = NewHome(TwoSources + "\n" + Mapping);
        var catalog = new PackageSourceCatalog(home.Provider);
        await Assert.That(catalog.SourcesFor("Northwind")).IsEmpty();
    }

    [Test]
    public async Task SourcesFor_WhenTheIdMapsToNoEnabledSource_IsEmpty()
    {
        using var home = NewHome(TwoSources + "\n" + OtherDisabled + "\n" + MappingToTheDisabledSource);
        var catalog = new PackageSourceCatalog(home.Provider);
        await Assert.That(catalog.SourcesFor("Contoso.Widgets")).IsEmpty();
    }

    private static TempHome NewHome(string sections)
    {
        const string prologue = """
                                <?xml version="1.0" encoding="utf-8"?>
                                <configuration>

                                """;

        const string epilogue = """

                                </configuration>
                                """;

        var home = new TempHome();
        home.WriteFile(ConfigFileName, prologue + sections + epilogue);
        return home;
    }
}
