// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.IO;

internal sealed class CaseSensitivityModeExtensionsTests
{
    [Test]
    public async Task IgnoresCase_WithCaseSensitive_ReturnsFalse()
    {
        await Assert.That(CaseSensitivityMode.CaseSensitive.IgnoresCase()).IsFalse();
    }

    [Test]
    public async Task IgnoresCase_WithCaseInsensitive_ReturnsTrue()
    {
        await Assert.That(CaseSensitivityMode.CaseInsensitive.IgnoresCase()).IsTrue();
    }

    [Test]
    public async Task IgnoresCase_WithSystemDefault_TracksOperatingSystem()
    {
        var expected = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

        await Assert.That(CaseSensitivityMode.SystemDefault.IgnoresCase()).IsEqualTo(expected);
    }

    [Test]
    public async Task IgnoresCase_WithUnknownValue_Throws()
    {
        static bool Act() => ((CaseSensitivityMode)999).IgnoresCase();

        await Assert.That(Act).Throws<ArgumentOutOfRangeException>();
    }
}
