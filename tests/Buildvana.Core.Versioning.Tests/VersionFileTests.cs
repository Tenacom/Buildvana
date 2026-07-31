// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.Testing;
using Buildvana.Core.Versioning;

internal sealed class VersionFileTests
{
    [Test]
    public async Task Load_MissingFile_Throws()
    {
        using var home = new TempHome();
        var exception = CatchLoad(home);
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("not found");
    }

    [Test]
    public async Task Load_MalformedFile_Throws()
    {
        using var home = new TempHome();
        home.WriteFile(VersionFile.FileName, "banana\n");
        var exception = CatchLoad(home);
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("banana");
    }

    [Test]
    public async Task Load_ValidFile_ParsesSpecAndExposesPath()
    {
        using var home = new TempHome();
        home.WriteFile(VersionFile.FileName, "2.3-preview\n");
        var versionFile = VersionFile.Load(home.Provider);
        await Assert.That(versionFile.Spec).IsEqualTo(new VersionSpec(2, 3, true));
        await Assert.That(versionFile.Path).IsEqualTo(Path.Combine(home.RootPath, VersionFile.FileName));
    }

    [Test]
    public async Task ApplyChange_UpdatesSpecAndReportsChange()
    {
        using var home = new TempHome();
        home.WriteFile(VersionFile.FileName, "2.3-x\n");
        var versionFile = VersionFile.Load(home.Provider);
        await Assert.That(versionFile.ApplyChange(VersionSpecChange.Stable)).IsTrue();
        await Assert.That(versionFile.Spec).IsEqualTo(new VersionSpec(2, 3, false));
        await Assert.That(versionFile.ApplyChange(VersionSpecChange.Stable)).IsFalse();
    }

    [Test]
    public async Task Save_StableSpec_WritesCanonicalForm()
    {
        using var home = new TempHome();
        home.WriteFile(VersionFile.FileName, "2.3\n");
        VersionFile.Load(home.Provider).Save("preview");
        await Assert.That(home.ReadFile(VersionFile.FileName)).IsEqualTo("2.3\n");
    }

    [Test]
    public async Task Save_PrereleaseSpecWithConformingTag_WritesTag()
    {
        using var home = new TempHome();
        home.WriteFile(VersionFile.FileName, "2.3-\n");
        VersionFile.Load(home.Provider).Save("preview");
        await Assert.That(home.ReadFile(VersionFile.FileName)).IsEqualTo("2.3-preview\n");
    }

    [Test]
    public async Task Save_PrereleaseSpecWithNoTag_WritesBareMarker()
    {
        using var home = new TempHome();
        home.WriteFile(VersionFile.FileName, "2.3-x\n");
        VersionFile.Load(home.Provider).Save();
        await Assert.That(home.ReadFile(VersionFile.FileName)).IsEqualTo("2.3-\n");
    }

    [Test]
    public async Task Save_PrereleaseSpecWithNonConformingTag_WritesBareMarker()
    {
        using var home = new TempHome();
        home.WriteFile(VersionFile.FileName, "2.3-x\n");
        VersionFile.Load(home.Provider).Save("beta.1");
        await Assert.That(home.ReadFile(VersionFile.FileName)).IsEqualTo("2.3-\n");
    }

    [Test]
    public async Task Save_AfterApplyChange_RoundTrips()
    {
        using var home = new TempHome();
        home.WriteFile(VersionFile.FileName, "2.3\n");
        var versionFile = VersionFile.Load(home.Provider);
        _ = versionFile.ApplyChange(VersionSpecChange.Minor);
        versionFile.Save("preview");
        var reloaded = VersionFile.Load(home.Provider);
        await Assert.That(reloaded.Spec).IsEqualTo(new VersionSpec(2, 4, true));
    }

    private static BuildFailedException? CatchLoad(TempHome home)
    {
        try
        {
            _ = VersionFile.Load(home.Provider);
            return null;
        }
        catch (BuildFailedException e)
        {
            return e;
        }
    }
}
