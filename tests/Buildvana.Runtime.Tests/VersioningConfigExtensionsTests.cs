// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Runtime;

internal sealed class VersioningConfigExtensionsTests
{
    // An absent section and an absent setting are the same statement — "not configured" — and the default
    // answers both, so that no consumer has to spell out the fallback and risk spelling it differently.
    [Test]
    public async Task EffectiveAssemblyVersionPrecision_WithoutSection_IsTheDefault()
    {
        VersioningConfig? config = null;

        await Assert.That(config.EffectiveAssemblyVersionPrecision)
            .IsEqualTo(VersioningConfig.DefaultAssemblyVersionPrecision);
    }

    [Test]
    public async Task EffectiveAssemblyVersionPrecision_WithoutValue_IsTheDefault()
    {
        var config = new VersioningConfig();

        await Assert.That(config.EffectiveAssemblyVersionPrecision)
            .IsEqualTo(VersioningConfig.DefaultAssemblyVersionPrecision);
    }

    [Test]
    public async Task EffectiveAssemblyVersionPrecision_WithValue_IsThatValue()
    {
        var config = new VersioningConfig { AssemblyVersionPrecision = AssemblyVersionPrecision.Build };

        await Assert.That(config.EffectiveAssemblyVersionPrecision).IsEqualTo(AssemblyVersionPrecision.Build);
    }
}
