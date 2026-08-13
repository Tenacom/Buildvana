// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Infrastructure.Execution;

internal sealed class ImplementsCommandAttributeTests
{
    [Test]
    public async Task Aliases_SplitIntoPaths_FirstIsCanonical()
    {
        var attribute = new ImplementsCommandAttribute("version show | version");
        await Assert.That(attribute.Aliases).IsEqualTo("version show | version");
        await Assert.That(attribute.AliasPaths.Count).IsEqualTo(2);
        await Assert.That(string.Join(' ', attribute.AliasPaths[0])).IsEqualTo("version show");
        await Assert.That(string.Join(' ', attribute.AliasPaths[1])).IsEqualTo("version");
    }

    [Test]
    public async Task Aliases_AreNormalizedToLowercase()
    {
        var attribute = new ImplementsCommandAttribute("Version  SHOW");
        await Assert.That(string.Join(' ', attribute.AliasPaths[0])).IsEqualTo("version show");
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments("foo |")]
    [Arguments("| foo")]
    [Arguments("foo | | bar")]
    public async Task EmptyAliases_Throw(string aliases)
    {
        await Assert.That(() => new ImplementsCommandAttribute(aliases)).Throws<ArgumentException>();
    }
}
