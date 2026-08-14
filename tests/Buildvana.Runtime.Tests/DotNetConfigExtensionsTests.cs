// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Runtime;

internal sealed class DotNetConfigExtensionsTests
{
    // An absent section and an absent setting are the same statement — "not configured" — and the default
    // answers both, so that no consumer has to spell out the fallback and risk spelling it differently.
    [Test]
    public async Task EffectiveConfiguration_WithoutSection_IsTheDefault()
    {
        DotNetConfig? config = null;

        await Assert.That(config.EffectiveConfiguration).IsEqualTo(DotNetConfig.DefaultConfiguration);
    }

    [Test]
    public async Task EffectiveConfiguration_WithoutValue_IsTheDefault()
    {
        var config = new DotNetConfig();

        await Assert.That(config.EffectiveConfiguration).IsEqualTo(DotNetConfig.DefaultConfiguration);
    }

    [Test]
    public async Task EffectiveConfiguration_WithValue_IsThatValue()
    {
        var config = new DotNetConfig { Configuration = "Debug" };

        await Assert.That(config.EffectiveConfiguration).IsEqualTo("Debug");
    }
}
