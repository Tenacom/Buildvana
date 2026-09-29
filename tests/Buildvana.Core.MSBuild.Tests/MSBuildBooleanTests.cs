// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.MSBuild;

internal sealed class MSBuildBooleanTests
{
    [Test]
    [Arguments("true")]
    [Arguments("on")]
    [Arguments("yes")]
    [Arguments("!false")]
    [Arguments("!off")]
    [Arguments("!no")]
    [Arguments("TRUE")]
    [Arguments("Yes")]
    [Arguments("!oFF")]
    public async Task TryParse_TrueKeyword_ReturnsTrue(string value)
    {
        var parsed = MSBuildBoolean.TryParse(value, out var result);
        await Assert.That(parsed).IsTrue();
        await Assert.That(result).IsTrue();
        await Assert.That(MSBuildBoolean.IsTrue(value)).IsTrue();
        await Assert.That(MSBuildBoolean.IsFalse(value)).IsFalse();
    }

    [Test]
    [Arguments("false")]
    [Arguments("off")]
    [Arguments("no")]
    [Arguments("!true")]
    [Arguments("!on")]
    [Arguments("!yes")]
    [Arguments("FALSE")]
    [Arguments("No")]
    [Arguments("!yES")]
    public async Task TryParse_FalseKeyword_ReturnsFalse(string value)
    {
        var parsed = MSBuildBoolean.TryParse(value, out var result);
        await Assert.That(parsed).IsTrue();
        await Assert.That(result).IsFalse();
        await Assert.That(MSBuildBoolean.IsTrue(value)).IsFalse();
        await Assert.That(MSBuildBoolean.IsFalse(value)).IsTrue();
    }

    // ConversionUtilities compares the whole string to each keyword, so a space or a second '!' makes it no keyword.
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("maybe")]
    [Arguments("1")]
    [Arguments(" true")]
    [Arguments("!!true")]
    [Arguments("! true")]
    public async Task TryParse_OtherValue_ReturnsNeither(string? value)
    {
        var parsed = MSBuildBoolean.TryParse(value, out var result);
        await Assert.That(parsed).IsFalse();
        await Assert.That(result).IsFalse();
        await Assert.That(MSBuildBoolean.IsTrue(value)).IsFalse();
        await Assert.That(MSBuildBoolean.IsFalse(value)).IsFalse();
    }
}
