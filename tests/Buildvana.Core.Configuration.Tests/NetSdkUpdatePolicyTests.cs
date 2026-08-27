// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Configuration;

internal sealed class NetSdkUpdatePolicyTests
{
    [Test]
    [Arguments("disable", NetSdkUpdatePolicyKind.Disable)]
    [Arguments("patch", NetSdkUpdatePolicyKind.Patch)]
    [Arguments("feature", NetSdkUpdatePolicyKind.Feature)]
    [Arguments("minor", NetSdkUpdatePolicyKind.Minor)]
    [Arguments("major", NetSdkUpdatePolicyKind.Major)]
    [Arguments("lts", NetSdkUpdatePolicyKind.Lts)]
    public async Task TryParse_WithKindName_YieldsStableOnlyPolicy(string text, NetSdkUpdatePolicyKind expected)
    {
        var parsed = NetSdkUpdatePolicy.TryParse(text, out var policy);

        await Assert.That(parsed).IsTrue();
        await Assert.That(policy.Kind).IsEqualTo(expected);
        await Assert.That(policy.AllowPrerelease).IsFalse();
    }

    [Test]
    [Arguments("patch-", NetSdkUpdatePolicyKind.Patch)]
    [Arguments("lts-", NetSdkUpdatePolicyKind.Lts)]
    public async Task TryParse_WithSuffix_AllowsPrerelease(string text, NetSdkUpdatePolicyKind expected)
    {
        var parsed = NetSdkUpdatePolicy.TryParse(text, out var policy);

        await Assert.That(parsed).IsTrue();
        await Assert.That(policy.Kind).IsEqualTo(expected);
        await Assert.That(policy.AllowPrerelease).IsTrue();
    }

    [Test]
    [Arguments("LTS")]
    [Arguments("Lts")]
    [Arguments("lTs")]
    public async Task TryParse_IgnoresCase(string text)
    {
        var parsed = NetSdkUpdatePolicy.TryParse(text, out var policy);

        await Assert.That(parsed).IsTrue();
        await Assert.That(policy.Kind).IsEqualTo(NetSdkUpdatePolicyKind.Lts);
    }

    // Exact is absent from this enum: patch with the suffix already names the RC-to-GA move.
    [Test]
    [Arguments("exact")]
    [Arguments("exact-")]
    public async Task TryParse_WithPackageOnlyKindName_Fails(string text)
    {
        await Assert.That(NetSdkUpdatePolicy.TryParse(text, out _)).IsFalse();
    }

    [Test]
    [Arguments("")]
    [Arguments("-")]
    [Arguments("lts--")]
    [Arguments("2")]
    [Arguments("lts,major")]
    public async Task TryParse_WithTextThatIsNotAPolicyString_Fails(string text)
    {
        var parsed = NetSdkUpdatePolicy.TryParse(text, out var policy);

        await Assert.That(parsed).IsFalse();
        await Assert.That(policy).IsEqualTo(default(NetSdkUpdatePolicy));
    }

    [Test]
    public async Task TryParse_WithNull_Fails()
    {
        var parsed = NetSdkUpdatePolicy.TryParse(null, out var policy);

        await Assert.That(parsed).IsFalse();
        await Assert.That(policy).IsEqualTo(default(NetSdkUpdatePolicy));
    }

    [Test]
    [Arguments("disable")]
    [Arguments("patch-")]
    [Arguments("feature")]
    [Arguments("minor")]
    [Arguments("major")]
    [Arguments("lts-")]
    public async Task ToString_RoundTripsThePolicyString(string text)
    {
        var parsed = NetSdkUpdatePolicy.TryParse(text, out var policy);

        await Assert.That(parsed).IsTrue();
        await Assert.That(policy.ToString()).IsEqualTo(text);
    }

    // Disable is the zero value so that a default-constructed policy moves nothing.
    [Test]
    public async Task Default_MovesNothing()
    {
        var policy = default(NetSdkUpdatePolicy);

        await Assert.That(policy.Kind).IsEqualTo(NetSdkUpdatePolicyKind.Disable);
        await Assert.That(policy.AllowPrerelease).IsFalse();
    }
}
