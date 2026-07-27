// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.Versioning;

internal sealed class VersionSpecParseTests
{
    [Test]
    [Arguments("1.0", 1, 0, false)]
    [Arguments("0.0", 0, 0, false)]
    [Arguments("10.20", 10, 20, false)]
    [Arguments("1.0-preview", 1, 0, true)]
    [Arguments("1.0-", 1, 0, true)]
    [Arguments("3.14-rc2", 3, 14, true)]
    [Arguments("1.0\n", 1, 0, false)]
    [Arguments("2.3-preview\r\n", 2, 3, true)]
    [Arguments("1.0 ", 1, 0, false)]
    public async Task TryParse_ValidInput_Parses(string input, int major, int minor, bool prerelease)
    {
        var parsed = VersionSpec.TryParse(input, out var result);
        await Assert.That(parsed).IsTrue();
        await Assert.That(result).IsEqualTo(new VersionSpec(major, minor, prerelease));
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("1")]
    [Arguments("1.")]
    [Arguments("1.0.3")]
    [Arguments("v1.0")]
    [Arguments("01.2")]
    [Arguments("1.02")]
    [Arguments(" 1.0")]
    [Arguments("1.0 garbage")]
    [Arguments("1.0-pre.view")]
    [Arguments("1.0-pre_view")]
    [Arguments("-1.0")]
    [Arguments("banana")]
    [Arguments("99999999999999999999.0")]
    public async Task TryParse_InvalidInput_ReturnsFalse(string? input)
    {
        var parsed = VersionSpec.TryParse(input, out _);
        await Assert.That(parsed).IsFalse();
    }

    [Test]
    public async Task Parse_ValidInput_Parses()
    {
        await Assert.That(VersionSpec.Parse("2.1-foo")).IsEqualTo(new VersionSpec(2, 1, true));
    }

    [Test]
    public async Task Parse_InvalidInput_ThrowsBuildFailedException()
    {
        BuildFailedException? exception = null;
        try
        {
            _ = VersionSpec.Parse("banana");
        }
        catch (BuildFailedException e)
        {
            exception = e;
        }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("banana");
    }

    [Test]
    [Arguments("1.0")]
    [Arguments("2.3-")]
    public async Task ToString_RoundTripsThroughParse(string canonical)
    {
        var spec = VersionSpec.Parse(canonical);
        await Assert.That(spec.ToString()).IsEqualTo(canonical);
    }
}
