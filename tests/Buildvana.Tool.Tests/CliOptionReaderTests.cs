// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Tool.CommandLine;

internal sealed class CliOptionReaderTests
{
    [Test]
    public async Task ReadFlag_ReturnsTrueAndConsumes_WhenLongNamePresent()
    {
        var reader = new CliOptionReader(["--nologo", "build"]);
        var found = reader.ReadFlag("--nologo");
        await Assert.That(found).IsTrue();
        await Assert.That(Join(reader.Remaining)).IsEqualTo("build");
    }

    [Test]
    public async Task ReadFlag_ReturnsTrue_WhenShortNamePresent()
    {
        var reader = new CliOptionReader(["-x"]);
        var found = reader.ReadFlag("--example", "-x");
        await Assert.That(found).IsTrue();
        await Assert.That(reader.Remaining.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ReadFlag_ReturnsFalse_WhenAbsent()
    {
        var reader = new CliOptionReader(["build"]);
        var found = reader.ReadFlag("--nologo");
        await Assert.That(found).IsFalse();
        await Assert.That(Join(reader.Remaining)).IsEqualTo("build");
    }

    [Test]
    public async Task ReadFlag_IsCaseInsensitive()
    {
        var reader = new CliOptionReader(["--NoLogo"]);
        var found = reader.ReadFlag("--nologo");
        await Assert.That(found).IsTrue();
    }

    [Test]
    public async Task ReadValue_ReadsSpaceSeparatedForm()
    {
        var reader = new CliOptionReader(["--verbosity", "detailed", "build"]);
        var value = reader.ReadValue("--verbosity", "-v");
        await Assert.That(value).IsEqualTo("detailed");
        await Assert.That(Join(reader.Remaining)).IsEqualTo("build");
    }

    [Test]
    public async Task ReadValue_ReadsInlineForm()
    {
        var reader = new CliOptionReader(["--verbosity=detailed", "build"]);
        var value = reader.ReadValue("--verbosity", "-v");
        await Assert.That(value).IsEqualTo("detailed");
        await Assert.That(Join(reader.Remaining)).IsEqualTo("build");
    }

    [Test]
    public async Task ReadValue_ReadsShortInlineForm()
    {
        var reader = new CliOptionReader(["-v=detailed"]);
        var value = reader.ReadValue("--verbosity", "-v");
        await Assert.That(value).IsEqualTo("detailed");
        await Assert.That(reader.Remaining.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ReadValue_IsCaseInsensitive()
    {
        var reader = new CliOptionReader(["--Verbosity", "detailed"]);
        var value = reader.ReadValue("--verbosity", "-v");
        await Assert.That(value).IsEqualTo("detailed");
    }

    [Test]
    public async Task ReadValue_LastOccurrenceWins()
    {
        var reader = new CliOptionReader(["-v", "quiet", "--verbosity", "detailed"]);
        var value = reader.ReadValue("--verbosity", "-v");
        await Assert.That(value).IsEqualTo("detailed");
        await Assert.That(reader.Remaining.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ReadValue_ReturnsNull_WhenAbsent()
    {
        var reader = new CliOptionReader(["build"]);
        var value = reader.ReadValue("--verbosity", "-v");
        await Assert.That(value).IsNull();
        await Assert.That(Join(reader.Remaining)).IsEqualTo("build");
    }

    [Test]
    public async Task ReadValue_Throws_WhenSpaceFormHasNoValue()
    {
        await Assert.That(() => _ = new CliOptionReader(["build", "--verbosity"]).ReadValue("--verbosity", "-v"))
            .Throws<BuildFailedException>();
    }

    [Test]
    public async Task Remaining_PreservesOrderOfUnconsumedTokens()
    {
        var reader = new CliOptionReader(["-c", "Debug", "--nologo", "--bump", "minor"]);
        _ = reader.ReadFlag("--nologo");
        await Assert.That(Join(reader.Remaining)).IsEqualTo("-c|Debug|--bump|minor");
    }

    private static string Join(IReadOnlyList<string> tokens) => string.Join('|', tokens);
}
