// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Versioning;

internal sealed class VersionFileDataTests
{
    [Test]
    public async Task Parse_ValidStableVersion_ParsesAllFields()
    {
        var data = VersionFileData.Parse("""{ "major": 2, "minor": 1, "prerelease": false }""");
        await Assert.That(data.Major).IsEqualTo(2);
        await Assert.That(data.Minor).IsEqualTo(1);
        await Assert.That(data.Prerelease).IsFalse();
    }

    [Test]
    public async Task Parse_ValidPrerelease_SetsPrereleaseTrue()
    {
        var data = VersionFileData.Parse("""{ "major": 0, "minor": 0, "prerelease": true }""");
        await Assert.That(data.Prerelease).IsTrue();
    }

    [Test]
    public async Task Parse_NotValidJson_Throws()
    {
        await Assert.That(() => VersionFileData.Parse("{ not json")).Throws<FormatException>();
    }

    [Test]
    public async Task Parse_NotAnObject_Throws()
    {
        await Assert.That(() => VersionFileData.Parse("42")).Throws<FormatException>();
    }

    [Test]
    public async Task Parse_MissingMajor_Throws()
    {
        const string json = """{ "minor": 0, "prerelease": false }""";
        await Assert.That(() => VersionFileData.Parse(json)).Throws<FormatException>();
    }

    [Test]
    public async Task Parse_MissingPrerelease_Throws()
    {
        const string json = """{ "major": 0, "minor": 0 }""";
        await Assert.That(() => VersionFileData.Parse(json)).Throws<FormatException>();
    }

    [Test]
    public async Task Parse_MajorWrongType_Throws()
    {
        const string json = """{ "major": "2", "minor": 0, "prerelease": false }""";
        await Assert.That(() => VersionFileData.Parse(json)).Throws<FormatException>();
    }

    [Test]
    public async Task Parse_NegativeMajor_Throws()
    {
        const string json = """{ "major": -1, "minor": 0, "prerelease": false }""";
        await Assert.That(() => VersionFileData.Parse(json)).Throws<FormatException>();
    }

    [Test]
    public async Task Parse_NonIntegerMajor_Throws()
    {
        const string json = """{ "major": 2.5, "minor": 0, "prerelease": false }""";
        await Assert.That(() => VersionFileData.Parse(json)).Throws<FormatException>();
    }

    [Test]
    public async Task Parse_PrereleaseWrongType_Throws()
    {
        const string json = """{ "major": 0, "minor": 0, "prerelease": "yes" }""";
        await Assert.That(() => VersionFileData.Parse(json)).Throws<FormatException>();
    }
}
