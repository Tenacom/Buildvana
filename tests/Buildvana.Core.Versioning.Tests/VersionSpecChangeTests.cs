// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Versioning;

internal sealed class VersionSpecChangeTests
{
    [Test]
    public async Task Stable_OnPrerelease_ClearsPrereleaseMarker()
    {
        var result = new VersionSpec(2, 3, true).Stable();
        await Assert.That(result).IsEqualTo(new VersionSpec(2, 3, false));
    }

    [Test]
    public async Task Stable_OnStable_ReturnsEqualSpec()
    {
        var spec = new VersionSpec(2, 3, false);
        await Assert.That(spec.Stable()).IsEqualTo(spec);
    }

    [Test]
    public async Task Unstable_OnStable_SetsPrereleaseMarker()
    {
        var result = new VersionSpec(2, 3, false).Unstable();
        await Assert.That(result).IsEqualTo(new VersionSpec(2, 3, true));
    }

    [Test]
    public async Task Unstable_OnPrerelease_ReturnsEqualSpec()
    {
        var spec = new VersionSpec(2, 3, true);
        await Assert.That(spec.Unstable()).IsEqualTo(spec);
    }

    [Test]
    public async Task NextMinor_IncrementsMinorAndStartsPrerelease()
    {
        var result = new VersionSpec(2, 3, false).NextMinor();
        await Assert.That(result).IsEqualTo(new VersionSpec(2, 4, true));
    }

    [Test]
    public async Task NextMajor_IncrementsMajorResetsMinorAndStartsPrerelease()
    {
        var result = new VersionSpec(2, 3, false).NextMajor();
        await Assert.That(result).IsEqualTo(new VersionSpec(3, 0, true));
    }

    [Test]
    [Arguments(false, VersionSpecChange.None, 2, 3, false, false)]
    [Arguments(true, VersionSpecChange.None, 2, 3, true, false)]
    [Arguments(false, VersionSpecChange.Unstable, 2, 3, true, true)]
    [Arguments(true, VersionSpecChange.Unstable, 2, 3, true, false)]
    [Arguments(false, VersionSpecChange.Stable, 2, 3, false, false)]
    [Arguments(true, VersionSpecChange.Stable, 2, 3, false, true)]
    [Arguments(false, VersionSpecChange.Minor, 2, 4, true, true)]
    [Arguments(true, VersionSpecChange.Minor, 2, 4, true, true)]
    [Arguments(false, VersionSpecChange.Major, 3, 0, true, true)]
    [Arguments(true, VersionSpecChange.Major, 3, 0, true, true)]
    public async Task ApplyChange_ComputesExpectedResult(
        bool prerelease,
        VersionSpecChange change,
        int expectedMajor,
        int expectedMinor,
        bool expectedPrerelease,
        bool expectedChanged)
    {
        var (result, changed) = new VersionSpec(2, 3, prerelease).ApplyChange(change);
        await Assert.That(result).IsEqualTo(new VersionSpec(expectedMajor, expectedMinor, expectedPrerelease));
        await Assert.That(changed).IsEqualTo(expectedChanged);
    }
}
