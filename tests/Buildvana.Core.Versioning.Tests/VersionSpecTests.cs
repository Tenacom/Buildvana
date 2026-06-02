// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Versioning;

internal sealed class VersionSpecTests
{
    [Test]
    public async Task TryParse_ValidStable_ParsesComponents()
    {
        var parsed = VersionSpec.TryParse("2.0", out var spec);
        await Assert.That(parsed).IsTrue();
        await Assert.That(spec!.Major).IsEqualTo(2);
        await Assert.That(spec.Minor).IsEqualTo(0);
        await Assert.That(spec.HasTag).IsFalse();
    }

    [Test]
    public async Task TryParse_WithVPrefixAndTag_ParsesComponents()
    {
        var parsed = VersionSpec.TryParse("v1.5-preview", out var spec);
        await Assert.That(parsed).IsTrue();
        await Assert.That(spec!.Major).IsEqualTo(1);
        await Assert.That(spec.Minor).IsEqualTo(5);
        await Assert.That(spec.Tag).IsEqualTo("preview");
    }

    [Test]
    public async Task TryParse_InvalidString_ReturnsFalse()
    {
        await Assert.That(VersionSpec.TryParse("not-a-version", out _)).IsFalse();
    }

    [Test]
    public async Task ToString_RoundTripsTag()
    {
        _ = VersionSpec.TryParse("3.4-beta", out var spec);
        await Assert.That(spec!.ToString()).IsEqualTo("3.4-beta");
    }

    [Test]
    public async Task ApplyChange_Major_BumpsMajorResetsMinorAddsTag()
    {
        _ = VersionSpec.TryParse("2.3", out var spec);
        var (result, changed) = spec!.ApplyChange(VersionSpecChange.Major, "preview");
        await Assert.That(changed).IsTrue();
        await Assert.That(result.ToString()).IsEqualTo("3.0-preview");
    }

    [Test]
    public async Task ApplyChange_StableWhenAlreadyStable_ReportsNoChange()
    {
        _ = VersionSpec.TryParse("2.3", out var spec);
        var (result, changed) = spec!.ApplyChange(VersionSpecChange.Stable, "preview");
        await Assert.That(changed).IsFalse();
        await Assert.That(result).IsEqualTo(spec);
    }
}
