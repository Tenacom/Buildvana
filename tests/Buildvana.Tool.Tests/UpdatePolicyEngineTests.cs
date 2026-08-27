// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Configuration;
using Buildvana.Tool.Services.Dependencies;
using NuGet.Versioning;

internal sealed class UpdatePolicyEngineTests
{
    [Test]
    public async Task SelectPackage_WithDisablePolicy_ReportsLatestWithoutATarget()
    {
        var selection = SelectPackage("disable", "1.2.3", "1.3.0", "2.0.0", "3.0.0-beta");

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.Disabled);
        await Assert.That(selection.Target).IsNull();
        await Assert.That(selection.LatestStable?.ToNormalizedString()).IsEqualTo("2.0.0");
        await Assert.That(selection.LatestPreview?.ToNormalizedString()).IsEqualTo("3.0.0-beta");
    }

    // A caller that resolves nothing for a disabled pin passes an empty candidate set.
    [Test]
    public async Task SelectPackage_WithDisablePolicyAndNoCandidates_ReportsNothing()
    {
        var selection = SelectPackage("disable", "1.2.3");

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.Disabled);
        await Assert.That(selection.Target).IsNull();
        await Assert.That(selection.LatestStable).IsNull();
        await Assert.That(selection.LatestPreview).IsNull();
    }

    [Test]
    public async Task SelectPackage_WithMajorPolicy_TakesTheHighestStable()
    {
        var selection = SelectPackage("major", "1.2.3", "1.2.3", "1.3.0", "2.0.0");

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.Update);
        await Assert.That(selection.Target?.ToNormalizedString()).IsEqualTo("2.0.0");
    }

    [Test]
    public async Task SelectPackage_WithMinorPolicy_StaysWithinTheMajor()
    {
        var selection = SelectPackage("minor", "1.2.3", "1.3.0", "2.0.0");

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.Update);
        await Assert.That(selection.Target?.ToNormalizedString()).IsEqualTo("1.3.0");
    }

    [Test]
    public async Task SelectPackage_WithPatchPolicy_StaysWithinTheMinor()
    {
        var selection = SelectPackage("patch", "1.2.3", "1.2.9", "1.3.0");

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.Update);
        await Assert.That(selection.Target?.ToNormalizedString()).IsEqualTo("1.2.9");
    }

    [Test]
    public async Task SelectPackage_WithExactPolicy_StaysWithinThePatch()
    {
        var selection = SelectPackage("exact", "1.2.3", "1.2.3", "1.2.4");

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.UpToDate);
        await Assert.That(selection.Target).IsNull();
    }

    // Exact fixes all four numeric fields, so a candidate that adds a revision is out of the window.
    [Test]
    public async Task SelectPackage_WithExactPolicy_RefusesARevisionMove()
    {
        var selection = SelectPackage("exact", "1.2.3", "1.2.3", "1.2.3.5");

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.UpToDate);
        await Assert.That(selection.Target).IsNull();
    }

    // The release counter of a four-field package is out of reach under exact, for the same reason.
    [Test]
    public async Task SelectPackage_WithExactPolicyAndFourFieldPin_KeepsTheRevision()
    {
        var selection = SelectPackage("exact", "3.7.400.55", "3.7.400.55", "3.7.400.56");

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.UpToDate);
        await Assert.That(selection.Target).IsNull();
    }

    // Stabilization moves no numeric field, so exact takes it on a four-field pin too.
    [Test]
    public async Task SelectPackage_WithExactPolicyAndFourFieldPin_Stabilizes()
    {
        var selection = SelectPackage("exact", "1.2.3.4-rc.1", "1.2.3.4-rc.1", "1.2.3.4");

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.Update);
        await Assert.That(selection.Target?.ToNormalizedString()).IsEqualTo("1.2.3.4");
    }

    [Test]
    public async Task SelectPackage_WithRevisionPolicy_StaysWithinThePatch()
    {
        var selection = SelectPackage("revision", "3.7.400.55", "3.7.400.56", "3.7.401.0");

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.Update);
        await Assert.That(selection.Target?.ToNormalizedString()).IsEqualTo("3.7.400.56");
    }

    // A three-field pin has revision zero, so a package that starts using the fourth field is still in reach.
    [Test]
    public async Task SelectPackage_WithRevisionPolicyAndThreeFieldPin_TakesAFourFieldCandidate()
    {
        var selection = SelectPackage("revision", "1.2.3", "1.2.3.5", "1.2.4");

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.Update);
        await Assert.That(selection.Target?.ToNormalizedString()).IsEqualTo("1.2.3.5");
    }

    [Test]
    public async Task SelectPackage_WithPatchPolicy_CrossesRevisions()
    {
        var selection = SelectPackage("patch", "3.7.400.55", "3.7.400.56", "3.7.401.2");

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.Update);
        await Assert.That(selection.Target?.ToNormalizedString()).IsEqualTo("3.7.401.2");
    }

    [Test]
    public async Task SelectPackage_WithStableOnlyPolicy_IgnoresPrereleases()
    {
        var selection = SelectPackage("minor", "1.2.3", "1.2.3", "1.3.0-beta");

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.UpToDate);
        await Assert.That(selection.LatestPreview?.ToNormalizedString()).IsEqualTo("1.3.0-beta");
    }

    [Test]
    public async Task SelectPackage_AllowingPrerelease_TakesAPrerelease()
    {
        var selection = SelectPackage("minor-", "1.2.3", "1.2.3", "1.3.0-beta");

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.Update);
        await Assert.That(selection.Target?.ToNormalizedString()).IsEqualTo("1.3.0-beta");
    }

    // Stabilization is always allowed: the stable release outranks the prerelease pin by SemVer ordering.
    [Test]
    public async Task SelectPackage_WithPrereleasePin_Stabilizes()
    {
        var selection = SelectPackage("exact", "1.2.0-preview.1", "1.2.0-preview.1", "1.2.0");

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.Update);
        await Assert.That(selection.Target?.ToNormalizedString()).IsEqualTo("1.2.0");
    }

    [Test]
    public async Task SelectPackage_WithPrereleasePinAndNoStableRelease_Holds()
    {
        var selection = SelectPackage("exact", "1.2.0-preview.1", "1.2.0-preview.1", "1.2.0-preview.2");

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.Held);
        await Assert.That(selection.Target).IsNull();
    }

    // The shape of an unlisted pin: the sources know nothing at or above it, and it is not a downgrade.
    [Test]
    public async Task SelectPackage_WithNothingAtOrAboveThePin_Holds()
    {
        var selection = SelectPackage("patch", "1.2.5", "1.2.3", "1.2.4");

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.Held);
        await Assert.That(selection.Target).IsNull();
        await Assert.That(selection.LatestStable?.ToNormalizedString()).IsEqualTo("1.2.4");
    }

    [Test]
    public async Task SelectPackage_WithNoCandidates_Holds()
    {
        var selection = SelectPackage("major", "1.2.3");

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.Held);
        await Assert.That(selection.Target).IsNull();
        await Assert.That(selection.LatestStable).IsNull();
        await Assert.That(selection.LatestPreview).IsNull();
    }

    [Test]
    public async Task SelectPackage_ComparesByPrecedenceNotByText()
    {
        var selection = SelectPackage("major", "13.0", "13.0.0");

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.UpToDate);
        await Assert.That(selection.Target).IsNull();
    }

    [Test]
    public async Task SelectPackage_WithUnsortedRepeatedCandidates_TakesTheHighest()
    {
        var selection = SelectPackage("minor", "1.2.3", "1.3.0", "1.2.9", "1.3.0", "1.2.3");

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.Update);
        await Assert.That(selection.Target?.ToNormalizedString()).IsEqualTo("1.3.0");
    }

    // The latest columns show what lies beyond the policy, so they ignore the window.
    [Test]
    public async Task SelectPackage_ReportsTheLatestVersionsBeyondThePolicy()
    {
        var selection = SelectPackage("patch", "1.2.3", "1.2.4", "2.0.0", "3.0.0-beta");

        await Assert.That(selection.Target?.ToNormalizedString()).IsEqualTo("1.2.4");
        await Assert.That(selection.LatestStable?.ToNormalizedString()).IsEqualTo("2.0.0");
        await Assert.That(selection.LatestPreview?.ToNormalizedString()).IsEqualTo("3.0.0-beta");
    }

    [Test]
    public async Task SelectPackage_WithNullCurrent_Throws()
    {
        static TargetSelection Act()
            => UpdatePolicyEngine.Select(null!, Versions("1.0.0"), new(PackageUpdatePolicyKind.Minor, false));

        await Assert.That(Act).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task SelectPackage_WithNullCandidates_Throws()
    {
        static TargetSelection Act()
            => UpdatePolicyEngine.Select(
                NuGetVersion.Parse("1.0.0"),
                (IReadOnlyCollection<NuGetVersion>)null!,
                new(PackageUpdatePolicyKind.Minor, false));

        await Assert.That(Act).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task SelectNetSdk_WithDisablePolicy_ReportsLatestWithoutATarget()
    {
        var selection = SelectNetSdk("disable", "10.0.402", Lts("10.0.403"));

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.Disabled);
        await Assert.That(selection.Target).IsNull();
        await Assert.That(selection.LatestStable?.ToNormalizedString()).IsEqualTo("10.0.403");
    }

    // The patch field encodes featureBand * 100 + patch, so 10.0.502 is another band, not another patch.
    [Test]
    public async Task SelectNetSdk_WithPatchPolicy_StaysWithinTheFeatureBand()
    {
        var selection = SelectNetSdk("patch", "10.0.402", Lts("10.0.403"), Lts("10.0.502"));

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.Update);
        await Assert.That(selection.Target?.ToNormalizedString()).IsEqualTo("10.0.403");
    }

    [Test]
    public async Task SelectNetSdk_WithFeaturePolicy_CrossesFeatureBands()
    {
        var selection = SelectNetSdk("feature", "10.0.402", Lts("10.0.502"), Lts("10.1.100"));

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.Update);
        await Assert.That(selection.Target?.ToNormalizedString()).IsEqualTo("10.0.502");
    }

    [Test]
    public async Task SelectNetSdk_WithMinorPolicy_StaysWithinTheMajor()
    {
        var selection = SelectNetSdk("minor", "10.0.402", Lts("10.1.100"), Sts("11.0.100"));

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.Update);
        await Assert.That(selection.Target?.ToNormalizedString()).IsEqualTo("10.1.100");
    }

    [Test]
    public async Task SelectNetSdk_WithMajorPolicy_TakesTheHighest()
    {
        var selection = SelectNetSdk("major", "10.0.402", Lts("10.1.100"), Sts("11.0.100"));

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.Update);
        await Assert.That(selection.Target?.ToNormalizedString()).IsEqualTo("11.0.100");
    }

    [Test]
    public async Task SelectNetSdk_WithStableOnlyPolicy_IgnoresPrereleases()
    {
        var selection = SelectNetSdk("major", "10.0.402", Lts("10.0.403"), Lts("11.0.100-preview.1"));

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.Update);
        await Assert.That(selection.Target?.ToNormalizedString()).IsEqualTo("10.0.403");
        await Assert.That(selection.LatestPreview?.ToNormalizedString()).IsEqualTo("11.0.100-preview.1");
    }

    // Short-term support releases leave the candidate set, but stay in the latest columns.
    [Test]
    public async Task SelectNetSdk_WithLtsPolicy_IgnoresShortTermSupportReleases()
    {
        var selection = SelectNetSdk("lts", "10.0.402", Lts("12.0.100"), Sts("13.0.100"));

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.Update);
        await Assert.That(selection.Target?.ToNormalizedString()).IsEqualTo("12.0.100");
        await Assert.That(selection.LatestStable?.ToNormalizedString()).IsEqualTo("13.0.100");
    }

    // An STS pin above the latest LTS has no candidate: nothing to do, and no warning of its own.
    [Test]
    public async Task SelectNetSdk_WithLtsPolicyAndShortTermSupportPin_Holds()
    {
        var selection = SelectNetSdk("lts", "11.0.100", Lts("10.0.402"), Sts("11.0.100"));

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.Held);
        await Assert.That(selection.Target).IsNull();
    }

    [Test]
    public async Task SelectNetSdk_AllowingPrerelease_TracksAnUpcomingLtsRelease()
    {
        var selection = SelectNetSdk("lts-", "12.0.100-rc.1", Lts("12.0.100-rc.2"), Sts("13.0.100"));

        await Assert.That(selection.Outcome).IsEqualTo(TargetSelectionOutcome.Update);
        await Assert.That(selection.Target?.ToNormalizedString()).IsEqualTo("12.0.100-rc.2");
    }

    [Test]
    public async Task SelectNetSdk_WithNullCurrent_Throws()
    {
        static TargetSelection Act()
            => UpdatePolicyEngine.Select(null!, [Lts("10.0.402")], new(NetSdkUpdatePolicyKind.Major, false));

        await Assert.That(Act).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task SelectNetSdk_WithNullCandidates_Throws()
    {
        static TargetSelection Act()
            => UpdatePolicyEngine.Select(
                NuGetVersion.Parse("10.0.402"),
                (IReadOnlyCollection<NetSdkRelease>)null!,
                new(NetSdkUpdatePolicyKind.Major, false));

        await Assert.That(Act).Throws<ArgumentNullException>();
    }

    private static TargetSelection SelectPackage(string policy, string current, params string[] candidates)
    {
        var parsed = PackageUpdatePolicy.TryParse(policy, out var result)
            ? result
            : throw new ArgumentException("Not a package policy string.", nameof(policy));

        return UpdatePolicyEngine.Select(NuGetVersion.Parse(current), Versions(candidates), parsed);
    }

    private static TargetSelection SelectNetSdk(string policy, string current, params NetSdkRelease[] candidates)
    {
        var parsed = NetSdkUpdatePolicy.TryParse(policy, out var result)
            ? result
            : throw new ArgumentException("Not a .NET SDK policy string.", nameof(policy));

        return UpdatePolicyEngine.Select(NuGetVersion.Parse(current), candidates, parsed);
    }

    private static IReadOnlyCollection<NuGetVersion> Versions(params string[] versions)
        => [.. versions.Select(NuGetVersion.Parse)];

    private static NetSdkRelease Lts(string version) => new(NuGetVersion.Parse(version), IsLts: true);

    private static NetSdkRelease Sts(string version) => new(NuGetVersion.Parse(version), IsLts: false);
}
