// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Configuration;
using Buildvana.Runtime;

// Buildvana.Runtime cannot see the policy types — it is BCL-only — so its scope defaults are plain strings.
// These tests are what keeps them policy strings of the kind their position accepts.
internal sealed class DependencyScopesConfigTests
{
    [Test]
    public async Task Default_NetSdk_IsStableLatestVersion()
    {
        var parsed = NetSdkUpdatePolicy.TryParse(new DependencyScopesConfig().NetSdk, out var policy);

        await Assert.That(parsed).IsTrue();
        await Assert.That(policy.Kind).IsEqualTo(NetSdkUpdatePolicyKind.Major);
        await Assert.That(policy.AllowPrerelease).IsFalse();
    }

    [Test]
    public async Task Default_PackageScopes_AreStableLatestMinor()
    {
        var defaults = new DependencyScopesConfig();
        var stableMinor = new PackageUpdatePolicy(PackageUpdatePolicyKind.Minor, AllowPrerelease: false);

        await Assert.That(ParsePackagePolicy(defaults.Sdks)).IsEqualTo(stableMinor);
        await Assert.That(ParsePackagePolicy(defaults.Tools)).IsEqualTo(stableMinor);
        await Assert.That(ParsePackagePolicy(defaults.Packages)).IsEqualTo(stableMinor);
    }

    // Null for text that is no policy string at all, rather than the default policy TryParse yields, which
    // updates nothing and would fail the comparison on a value that hides the reason.
    private static PackageUpdatePolicy? ParsePackagePolicy(string text)
        => PackageUpdatePolicy.TryParse(text, out var policy) ? policy : null;
}
