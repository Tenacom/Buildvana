// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Configuration;

internal sealed class PackageUpdatePolicyTests
{
    [Test]
    [Arguments("disable", PackageUpdatePolicyKind.Disable)]
    [Arguments("exact", PackageUpdatePolicyKind.Exact)]
    [Arguments("patch", PackageUpdatePolicyKind.Patch)]
    [Arguments("minor", PackageUpdatePolicyKind.Minor)]
    [Arguments("major", PackageUpdatePolicyKind.Major)]
    public async Task TryParse_WithKindName_YieldsStableOnlyPolicy(string text, PackageUpdatePolicyKind expected)
    {
        var parsed = PackageUpdatePolicy.TryParse(text, out var policy);

        await Assert.That(parsed).IsTrue();
        await Assert.That(policy.Kind).IsEqualTo(expected);
        await Assert.That(policy.AllowPrerelease).IsFalse();
    }

    // The suffix parses on every kind, disable included; what an update makes of it is another matter.
    [Test]
    [Arguments("disable-", PackageUpdatePolicyKind.Disable)]
    [Arguments("exact-", PackageUpdatePolicyKind.Exact)]
    [Arguments("major-", PackageUpdatePolicyKind.Major)]
    public async Task TryParse_WithSuffix_AllowsPrerelease(string text, PackageUpdatePolicyKind expected)
    {
        var parsed = PackageUpdatePolicy.TryParse(text, out var policy);

        await Assert.That(parsed).IsTrue();
        await Assert.That(policy.Kind).IsEqualTo(expected);
        await Assert.That(policy.AllowPrerelease).IsTrue();
    }

    [Test]
    [Arguments("MINOR")]
    [Arguments("Minor")]
    [Arguments("mInOr")]
    public async Task TryParse_IgnoresCase(string text)
    {
        var parsed = PackageUpdatePolicy.TryParse(text, out var policy);

        await Assert.That(parsed).IsTrue();
        await Assert.That(policy.Kind).IsEqualTo(PackageUpdatePolicyKind.Minor);
    }

    // Each position parses against one enum, so a .NET SDK kind name is an error here.
    [Test]
    [Arguments("lts")]
    [Arguments("lts-")]
    [Arguments("feature")]
    public async Task TryParse_WithNetSdkKindName_Fails(string text)
    {
        await Assert.That(PackageUpdatePolicy.TryParse(text, out _)).IsFalse();
    }

    [Test]
    [Arguments("")]
    [Arguments(" ")]
    [Arguments("-")] // Nothing left once the suffix is stripped.
    [Arguments("minor--")] // Only one suffix is a suffix.
    [Arguments("minor ")]
    [Arguments(" minor")]
    [Arguments("mi nor")]
    [Arguments("minor.")]
    [Arguments("1")] // Enum.TryParse would take the numeric value.
    [Arguments("minor,major")] // Enum.TryParse would take the comma-separated list.
    [Arguments("mayor")]
    public async Task TryParse_WithTextThatIsNotAPolicyString_Fails(string text)
    {
        var parsed = PackageUpdatePolicy.TryParse(text, out var policy);

        await Assert.That(parsed).IsFalse();
        await Assert.That(policy).IsEqualTo(default(PackageUpdatePolicy));
    }

    [Test]
    public async Task TryParse_WithNull_Fails()
    {
        var parsed = PackageUpdatePolicy.TryParse(null, out var policy);

        await Assert.That(parsed).IsFalse();
        await Assert.That(policy).IsEqualTo(default(PackageUpdatePolicy));
    }

    [Test]
    [Arguments("disable")]
    [Arguments("exact-")]
    [Arguments("patch")]
    [Arguments("minor-")]
    [Arguments("major")]
    public async Task ToString_RoundTripsThePolicyString(string text)
    {
        var parsed = PackageUpdatePolicy.TryParse(text, out var policy);

        await Assert.That(parsed).IsTrue();
        await Assert.That(policy.ToString()).IsEqualTo(text);
    }

    // Disable is the zero value so that a default-constructed policy moves nothing.
    [Test]
    public async Task Default_MovesNothing()
    {
        var policy = default(PackageUpdatePolicy);

        await Assert.That(policy.Kind).IsEqualTo(PackageUpdatePolicyKind.Disable);
        await Assert.That(policy.AllowPrerelease).IsFalse();
    }
}
