// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Versioning;

internal sealed class VersionSpecTests
{
    [Test]
    public async Task ToString_StableSpec_EmitsMajorDotMinor()
    {
        await Assert.That(new VersionSpec(1, 0, false).ToString()).IsEqualTo("1.0");
    }

    [Test]
    public async Task ToString_PrereleaseSpec_EmitsTrailingDash()
    {
        await Assert.That(new VersionSpec(2, 3, true).ToString()).IsEqualTo("2.3-");
    }

    [Test]
    public async Task Equality_ComparesByValue()
    {
        await Assert.That(new VersionSpec(1, 2, true)).IsEqualTo(new VersionSpec(1, 2, true));
        await Assert.That(new VersionSpec(1, 2, true)).IsNotEqualTo(new VersionSpec(1, 2, false));
    }
}
