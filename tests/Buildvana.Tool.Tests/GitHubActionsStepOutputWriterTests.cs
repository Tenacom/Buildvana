// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Testing;
using Buildvana.Tool.Services.Platforms;

internal sealed class GitHubActionsStepOutputWriterTests
{
    [Test]
    public async Task Write_AppendsOneLinePerSingleLineValue()
    {
        using var home = new TempHome();
        var path = home.GetFullPath("output.txt");
        await File.WriteAllTextAsync(path, "earlier=output\n").ConfigureAwait(false);
        var writer = new GitHubActionsStepOutputWriter(path);

        writer.Write("version", "1.2.3");
        writer.Write("empty", string.Empty);

        string[] expected = ["earlier=output", "version=1.2.3", "empty="];
        await Assert.That(await File.ReadAllLinesAsync(path).ConfigureAwait(false)).IsEquivalentTo(expected);
    }

    // A byte order mark would come before the first output name.
    [Test]
    public async Task Write_ToNewFile_WritesNoByteOrderMark()
    {
        using var home = new TempHome();
        var path = home.GetFullPath("output.txt");

        new GitHubActionsStepOutputWriter(path).Write("version", "1.2.3");

        var bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
        await Assert.That(bytes[0]).IsEqualTo((byte)'v');
    }

    [Test]
    public async Task Write_MultiLineValue_UsesDelimiterForm()
    {
        using var home = new TempHome();
        var path = home.GetFullPath("output.txt");

        new GitHubActionsStepOutputWriter(path).Write("notes", "first\nsecond");

        var lines = await File.ReadAllLinesAsync(path).ConfigureAwait(false);
        await Assert.That(lines.Length).IsEqualTo(4);
        await Assert.That(lines[0]).StartsWith("notes<<");
        var delimiter = lines[0]["notes<<".Length..];
        await Assert.That(delimiter.Length).IsGreaterThan(0);
        await Assert.That(lines[1]).IsEqualTo("first");
        await Assert.That(lines[2]).IsEqualTo("second");
        await Assert.That(lines[3]).IsEqualTo(delimiter);
    }

    [Test]
    [Arguments("a=b")]
    [Arguments("a<b")]
    [Arguments("a\nb")]
    public async Task Write_NameWithSeparator_Throws(string name)
    {
        using var home = new TempHome();
        var writer = new GitHubActionsStepOutputWriter(home.GetFullPath("output.txt"));

        await Assert.That(() => writer.Write(name, "value")).Throws<ArgumentException>();
    }
}
