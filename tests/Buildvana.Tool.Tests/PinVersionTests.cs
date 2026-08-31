// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Services.Dependencies;

internal sealed class PinVersionTests
{
    [Test]
    [Arguments("13.0.3")]
    [Arguments("1.2.0-preview.1")]
    [Arguments("1.2.3.4")]
    [Arguments("13.0")]
    [Arguments("  13.0.3  ")] // a Version child element carries the whitespace around its value
    public async Task Read_OfAnExactVersion_IsManaged(string text)
    {
        await Assert.That(PinVersion.Read(text, out var version)).IsEqualTo(PinManagement.Managed);
        await Assert.That(version).IsNotNull();
    }

    [Test]
    [Arguments("[13.0.4]")]
    [Arguments("[13.0.4, 13.0.4]")]
    public async Task Read_OfTheBracketFormOfOneVersion_SaysSo(string text)
    {
        await Assert.That(PinVersion.Read(text, out var version)).IsEqualTo(PinManagement.BracketExactVersion);
        await Assert.That(version).IsNull();
    }

    [Test]
    [Arguments("[1.0,2.0)")]
    [Arguments("(1.0,)")]
    [Arguments("[1.0,]")]
    public async Task Read_OfARange_SaysSo(string text)
    {
        await Assert.That(PinVersion.Read(text, out _)).IsEqualTo(PinManagement.VersionRange);
    }

    [Test]
    [Arguments("1.*")]
    [Arguments("*")]
    [Arguments("1.2.*-*")]
    public async Task Read_OfAFloatingVersion_SaysSo(string text)
    {
        await Assert.That(PinVersion.Read(text, out _)).IsEqualTo(PinManagement.FloatingVersion);
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments(null)]
    [Arguments("not a version")]
    [Arguments("$(SerilogVersion)")]
    public async Task Read_OfAnythingElse_IsUnreadable(string? text)
    {
        await Assert.That(PinVersion.Read(text, out var version)).IsEqualTo(PinManagement.UnreadableVersion);
        await Assert.That(version).IsNull();
    }
}
