// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Buildvana.Core.Testing;
using Buildvana.Tool.Utilities;

internal sealed class MsBuildPinEditorTests
{
    private const string FileName = "Directory.Packages.props";

    [Test]
    public async Task ReadPins_FindsAttributeFormPins_InDocumentOrder()
    {
        using var home = new TempHome();
        const string content = """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Alpha" Version="1.0.0" />
                <PackageVersion Include="Beta" Version="2.0.0-preview.3" />
              </ItemGroup>
            </Project>
            """;
        var path = WriteFile(home, content);

        var pins = MsBuildPinEditor.ReadPins(path, ["PackageVersion"]);

        await Assert.That(pins).IsEquivalentTo([
            new MsBuildPin("PackageVersion", "Alpha", "1.0.0"),
            new MsBuildPin("PackageVersion", "Beta", "2.0.0-preview.3")]);
    }

    // dotnet package preserves reversed attribute order, so files it wrote must stay readable.
    [Test]
    public async Task ReadPins_FindsPins_RegardlessOfAttributeOrder()
    {
        using var home = new TempHome();
        const string content = """
            <Project>
              <ItemGroup>
                <PackageVersion Version="1.2.3" Include="Alpha" />
              </ItemGroup>
            </Project>
            """;
        var path = WriteFile(home, content);

        var pins = MsBuildPinEditor.ReadPins(path, ["PackageVersion"]);

        await Assert.That(pins).IsEquivalentTo([new MsBuildPin("PackageVersion", "Alpha", "1.2.3")]);
    }

    [Test]
    public async Task ReadPins_FindsPins_WithSingleQuotedAttributes()
    {
        using var home = new TempHome();
        const string content = """
            <Project>
              <ItemGroup>
                <PackageVersion Include='Alpha' Version='1.2.3' />
              </ItemGroup>
            </Project>
            """;
        var path = WriteFile(home, content);

        var pins = MsBuildPinEditor.ReadPins(path, ["PackageVersion"]);

        await Assert.That(pins).IsEquivalentTo([new MsBuildPin("PackageVersion", "Alpha", "1.2.3")]);
    }

    [Test]
    public async Task ReadPins_FindsVersionChildElements()
    {
        using var home = new TempHome();
        const string content = """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Alpha">
                  <Version>1.2.3</Version>
                </PackageVersion>
              </ItemGroup>
            </Project>
            """;
        var path = WriteFile(home, content);

        var pins = MsBuildPinEditor.ReadPins(path, ["PackageVersion"]);

        await Assert.That(pins).IsEquivalentTo([new MsBuildPin("PackageVersion", "Alpha", "1.2.3")]);
    }

    [Test]
    public async Task ReadPins_PrefersTheVersionAttribute_OverAVersionChildElement()
    {
        using var home = new TempHome();
        const string content = """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Alpha" Version="1.0.0">
                  <Version>9.9.9</Version>
                </PackageVersion>
              </ItemGroup>
            </Project>
            """;
        var path = WriteFile(home, content);

        var pins = MsBuildPinEditor.ReadPins(path, ["PackageVersion"]);

        await Assert.That(pins).IsEquivalentTo([new MsBuildPin("PackageVersion", "Alpha", "1.0.0")]);
    }

    // A Version child whose content is not plain text up to its end tag is unusable, and yields no pin.
    [Test]
    public async Task ReadPins_IgnoresAVersionChildContainingMarkup()
    {
        using var home = new TempHome();
        const string content = """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Alpha">
                  <Version>1.0.0<!-- pinned --></Version>
                </PackageVersion>
              </ItemGroup>
            </Project>
            """;
        var path = WriteFile(home, content);

        var pins = MsBuildPinEditor.ReadPins(path, ["PackageVersion"]);

        await Assert.That(pins.Count).IsEqualTo(0);
    }

    // Update-form items are invisible by design: whoever manages references through Update items manages
    // their versions too.
    [Test]
    public async Task ReadPins_IgnoresUpdateFormItems()
    {
        using var home = new TempHome();
        const string content = """
            <Project>
              <ItemGroup>
                <PackageReference Update="Alpha" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """;
        var path = WriteFile(home, content);

        var pins = MsBuildPinEditor.ReadPins(path, ["PackageReference"]);

        await Assert.That(pins.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ReadPins_IgnoresItemsWithoutAVersion()
    {
        using var home = new TempHome();
        const string content = """
            <Project>
              <ItemGroup>
                <PackageReference Include="Alpha" />
                <PackageReference Include="Beta">
                  <PrivateAssets>all</PrivateAssets>
                </PackageReference>
              </ItemGroup>
            </Project>
            """;
        var path = WriteFile(home, content);

        var pins = MsBuildPinEditor.ReadPins(path, ["PackageReference"]);

        await Assert.That(pins.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ReadPins_IgnoresCommentedOutItems()
    {
        using var home = new TempHome();
        const string content = """
            <Project>
              <ItemGroup>
                <!-- <PackageVersion Include="Alpha" Version="1.0.0" /> -->
                <PackageVersion Include="Beta" Version="2.0.0" />
              </ItemGroup>
            </Project>
            """;
        var path = WriteFile(home, content);

        var pins = MsBuildPinEditor.ReadPins(path, ["PackageVersion"]);

        await Assert.That(pins).IsEquivalentTo([new MsBuildPin("PackageVersion", "Beta", "2.0.0")]);
    }

    [Test]
    public async Task ReadPins_IgnoresItemsInsideACdataSection()
    {
        using var home = new TempHome();
        const string content = """
            <Project>
              <ItemGroup>
                <![CDATA[ <PackageVersion Include="Alpha" Version="1.0.0" /> ]]>
                <PackageVersion Include="Beta" Version="2.0.0" />
              </ItemGroup>
            </Project>
            """;
        var path = WriteFile(home, content);

        var pins = MsBuildPinEditor.ReadPins(path, ["PackageVersion"]);

        await Assert.That(pins).IsEquivalentTo([new MsBuildPin("PackageVersion", "Beta", "2.0.0")]);
    }

    [Test]
    public async Task ReadPins_IgnoresItemsInsideAProcessingInstruction()
    {
        using var home = new TempHome();
        const string content = """
            <Project>
              <ItemGroup>
                <?pi <PackageVersion Include="Alpha" Version="1.0.0" /> ?>
                <PackageVersion Include="Beta" Version="2.0.0" />
              </ItemGroup>
            </Project>
            """;
        var path = WriteFile(home, content);

        var pins = MsBuildPinEditor.ReadPins(path, ["PackageVersion"]);

        await Assert.That(pins).IsEquivalentTo([new MsBuildPin("PackageVersion", "Beta", "2.0.0")]);
    }

    [Test]
    public async Task ReadPins_ReadsOnlyTheWantedItemTypes()
    {
        using var home = new TempHome();
        const string content = """
            <Project>
              <ItemGroup>
                <GlobalPackageReference Include="Alpha" Version="1.0.0" />
                <PackageVersion Include="Beta" Version="2.0.0" />
              </ItemGroup>
            </Project>
            """;
        var path = WriteFile(home, content);

        var pins = MsBuildPinEditor.ReadPins(path, ["GlobalPackageReference"]);

        await Assert.That(pins).IsEquivalentTo([new MsBuildPin("GlobalPackageReference", "Alpha", "1.0.0")]);
    }

    // MSBuild treats item-type names case-insensitively; so does the editor. The pin reports the file's
    // own spelling.
    [Test]
    public async Task ReadPins_MatchesItemTypesCaseInsensitively()
    {
        using var home = new TempHome();
        const string content = """
            <Project>
              <ItemGroup>
                <packageVERSION Include="Alpha" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """;
        var path = WriteFile(home, content);

        var pins = MsBuildPinEditor.ReadPins(path, ["PackageVersion"]);

        await Assert.That(pins).IsEquivalentTo([new MsBuildPin("packageVERSION", "Alpha", "1.0.0")]);
    }

    // XML allows a raw '>' inside a quoted attribute value, and MSBuild conditions actually use one; a
    // scanner that searched for the tag's '>' would break here.
    [Test]
    public async Task ReadPins_ParsesAConditionContainingAGreaterThanSign()
    {
        using var home = new TempHome();
        const string content = """
            <Project>
              <ItemGroup>
                <PackageVersion Condition="'$(N)' > '1'" Include="Alpha" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """;
        var path = WriteFile(home, content);

        var pins = MsBuildPinEditor.ReadPins(path, ["PackageVersion"]);

        await Assert.That(pins).IsEquivalentTo([new MsBuildPin("PackageVersion", "Alpha", "1.0.0")]);
    }

    [Test]
    public async Task ReadPins_ReturnsPropertyReferenceVersionsVerbatim()
    {
        using var home = new TempHome();
        const string content = """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Alpha" Version="$(AlphaVersion)" />
              </ItemGroup>
            </Project>
            """;
        var path = WriteFile(home, content);

        var pins = MsBuildPinEditor.ReadPins(path, ["PackageVersion"]);

        await Assert.That(pins).IsEquivalentTo([new MsBuildPin("PackageVersion", "Alpha", "$(AlphaVersion)")]);
    }

    // Two declarations of one id — e.g. conditioned per target framework — are two pins.
    [Test]
    public async Task ReadPins_ReturnsEveryDeclarationOfAnId()
    {
        using var home = new TempHome();
        const string content = """
            <Project>
              <ItemGroup>
                <PackageVersion Condition="'$(TargetFramework)' == 'net10.0'" Include="Alpha" Version="2.0.0" />
                <PackageVersion Condition="'$(TargetFramework)' == 'netstandard2.0'" Include="Alpha" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """;
        var path = WriteFile(home, content);

        var pins = MsBuildPinEditor.ReadPins(path, ["PackageVersion"]);

        await Assert.That(pins).IsEquivalentTo([
            new MsBuildPin("PackageVersion", "Alpha", "2.0.0"),
            new MsBuildPin("PackageVersion", "Alpha", "1.0.0")]);
    }

    // Malformed content never throws: what cannot be parsed is skipped. The unclosed quote swallows the
    // rest of the file from the scanner's point of view, so only the item before it is found.
    [Test]
    public async Task ReadPins_ToleratesMalformedContent()
    {
        using var home = new TempHome();
        const string content = """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Alpha" Version="1.0.0" />
                <PackageVersion Include="Broken Version=
              </ItemGroup>
            </Project>
            """;
        var path = WriteFile(home, content);

        var pins = MsBuildPinEditor.ReadPins(path, ["PackageVersion"]);

        await Assert.That(pins).IsEquivalentTo([new MsBuildPin("PackageVersion", "Alpha", "1.0.0")]);
    }

    [Test]
    public async Task RewritePins_SplicesOnlyTheVersions()
    {
        using var home = new TempHome();
        const string content = """
            <Project>
              <!-- Centrally managed versions. -->
              <ItemGroup Label="Run-time dependencies">
                <PackageVersion Include="Alpha" Version="1.0.0" />
                <PackageVersion Version='2.0.0' Include='Beta' Condition="'$(N)' > '1'" />
                <!-- <PackageVersion Include="Gamma" Version="9.9.9" /> -->
                <PackageVersion Include="Delta">
                  <Version>3.0.0</Version>
                </PackageVersion>
                <PackageVersion Include="Epsilon" Version="$(EpsilonVersion)" />
              </ItemGroup>
            </Project>
            """;
        var path = WriteFile(home, content);
        var newVersions = new Dictionary<string, string>
        {
            ["Alpha"] = "1.0.1",
            ["Beta"] = "2.0.1",
            ["Gamma"] = "9.9.10",
            ["Delta"] = "3.0.1",
        };

        var changed = MsBuildPinEditor.RewritePins(path, ["PackageVersion"], p => newVersions.GetValueOrDefault(p.Id));

        await Assert.That(changed).IsTrue();
        await Assert.That(home.ReadFile(FileName)).IsEqualTo("""
            <Project>
              <!-- Centrally managed versions. -->
              <ItemGroup Label="Run-time dependencies">
                <PackageVersion Include="Alpha" Version="1.0.1" />
                <PackageVersion Version='2.0.1' Include='Beta' Condition="'$(N)' > '1'" />
                <!-- <PackageVersion Include="Gamma" Version="9.9.9" /> -->
                <PackageVersion Include="Delta">
                  <Version>3.0.1</Version>
                </PackageVersion>
                <PackageVersion Include="Epsilon" Version="$(EpsilonVersion)" />
              </ItemGroup>
            </Project>
            """);
    }

    [Test]
    public async Task RewritePins_RewritesEveryDeclarationOfAnId()
    {
        using var home = new TempHome();
        const string content = """
            <Project>
              <ItemGroup>
                <PackageVersion Condition="'$(TargetFramework)' == 'net10.0'" Include="Alpha" Version="2.0.0" />
                <PackageVersion Condition="'$(TargetFramework)' == 'netstandard2.0'" Include="Alpha" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """;
        var path = WriteFile(home, content);

        var changed = MsBuildPinEditor.RewritePins(path, ["PackageVersion"], _ => "3.0.0");

        await Assert.That(changed).IsTrue();
        await Assert.That(home.ReadFile(FileName)).IsEqualTo("""
            <Project>
              <ItemGroup>
                <PackageVersion Condition="'$(TargetFramework)' == 'net10.0'" Include="Alpha" Version="3.0.0" />
                <PackageVersion Condition="'$(TargetFramework)' == 'netstandard2.0'" Include="Alpha" Version="3.0.0" />
              </ItemGroup>
            </Project>
            """);
    }

    [Test]
    [Arguments(null)]
    [Arguments("1.0.0")]
    public async Task RewritePins_LeavesTheFileAlone_WhenNothingChanges(string? newVersionText)
    {
        using var home = new TempHome();
        const string content = """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Alpha" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """;
        var path = WriteFile(home, content);

        var changed = MsBuildPinEditor.RewritePins(path, ["PackageVersion"], _ => newVersionText);

        await Assert.That(changed).IsFalse();
        await Assert.That(home.ReadFile(FileName)).IsEqualTo(content);
    }

    [Test]
    public async Task RewritePins_PreservesCrlfLineEndings()
    {
        using var home = new TempHome();
        var path = Path.Combine(home.RootPath, FileName);
        const string content = "<Project>\r\n  <PackageVersion Include=\"Alpha\" Version=\"1.0.0\" />\r\n</Project>\r\n";
        const string expected = "<Project>\r\n  <PackageVersion Include=\"Alpha\" Version=\"1.0.1\" />\r\n</Project>\r\n";
        await File.WriteAllTextAsync(path, content).ConfigureAwait(false);

        var changed = MsBuildPinEditor.RewritePins(path, ["PackageVersion"], _ => "1.0.1");

        await Assert.That(changed).IsTrue();
        var rewritten = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        await Assert.That(rewritten).IsEqualTo(expected);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task RewritePins_PreservesTheByteOrderMark(bool hasByteOrderMark)
    {
        using var home = new TempHome();
        var path = Path.Combine(home.RootPath, FileName);
        const string content = """
            <Project>
              <PackageVersion Include="Alpha" Version="1.0.0" />
            </Project>
            """;
        byte[] contentBytes = Encoding.UTF8.GetBytes(content);
        await File.WriteAllBytesAsync(path, hasByteOrderMark ? [0xEF, 0xBB, 0xBF, .. contentBytes] : contentBytes).ConfigureAwait(false);

        var changed = MsBuildPinEditor.RewritePins(path, ["PackageVersion"], _ => "1.0.1");

        await Assert.That(changed).IsTrue();
        var rewrittenBytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
        var hasMark = rewrittenBytes is [0xEF, 0xBB, 0xBF, ..];
        await Assert.That(hasMark).IsEqualTo(hasByteOrderMark);
    }

    private static string WriteFile(TempHome home, string content)
    {
        home.WriteFile(FileName, content);
        return Path.Combine(home.RootPath, FileName);
    }
}
