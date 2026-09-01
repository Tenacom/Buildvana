// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Configuration;
using Buildvana.Tool.Services.Dependencies;
using NuGet.Protocol;
using NuGet.Versioning;

internal sealed class OverrideSelectorTests
{
    [Test]
    public async Task Select_TakesTheLowestVersionOutsideEveryAdvisory()
    {
        var decision = OverrideSelector.Select(Request("12.0.1", ["12.0.1", "12.0.2", "12.0.3", "13.0.1"], Advisory("(, 12.0.2]")));
        await Assert.That(decision.Outcome).IsEqualTo(OverrideOutcome.Override);
        await Assert.That(decision.Version?.ToNormalizedString()).IsEqualTo("12.0.3");
    }

    // Every advisory counts, not only the one that covers the resolved version: a version chosen inside
    // another's range would be reported, and lifted again, on the next run.
    [Test]
    public async Task Select_SkipsAVersionAnotherAdvisoryCovers()
    {
        var decision = OverrideSelector.Select(
            Request("12.0.1", ["12.0.1", "12.0.2", "12.0.3"], Advisory("(, 12.0.1]"), Advisory("[12.0.2, 12.0.2]")));

        await Assert.That(decision.Version?.ToNormalizedString()).IsEqualTo("12.0.3");
    }

    [Test]
    public async Task Select_IgnoresAnAdvisoryBelowTheProjectsAuditLevel()
    {
        var request = Request("12.0.1", ["12.0.1", "12.0.3"], Advisory("(, 12.0.2]", PackageVulnerabilitySeverity.Low)) with
        {
            AuditLevel = PackageVulnerabilitySeverity.High,
        };

        var decision = OverrideSelector.Select(request);
        await Assert.That(decision.Outcome).IsEqualTo(OverrideOutcome.NoFix);
    }

    [Test]
    public async Task Select_WithNoSafeVersion_HasNoFix()
    {
        var decision = OverrideSelector.Select(Request("12.0.1", ["12.0.1", "12.0.2"], Advisory("(, 13.0.0)")));
        await Assert.That(decision.Outcome).IsEqualTo(OverrideOutcome.NoFix);
        await Assert.That(decision.Reason).Contains("outside every advisory");
    }

    [Test]
    public async Task Select_WithTheFixBeyondThePolicy_HasNoFixAndNamesTheVersion()
    {
        var request = Request("12.0.1", ["12.0.1", "13.0.1"], Advisory("(, 13.0.0)")) with { Policy = Policy("patch") };
        var decision = OverrideSelector.Select(request);
        await Assert.That(decision.Outcome).IsEqualTo(OverrideOutcome.NoFix);
        await Assert.That(decision.Reason).Contains("13.0.1");
        await Assert.That(decision.Reason).Contains("'patch'");
    }

    [Test]
    public async Task Select_WithAPrereleaseFixUnderAStablePolicy_HasNoFix()
    {
        var decision = OverrideSelector.Select(Request("12.0.1", ["12.0.1", "12.0.2-beta"], Advisory("(, 12.0.1]")));
        await Assert.That(decision.Outcome).IsEqualTo(OverrideOutcome.NoFix);
        await Assert.That(decision.Reason).Contains("prerelease");
    }

    [Test]
    public async Task Select_WithAPrereleaseFixUnderAPrereleasePolicy_TakesIt()
    {
        var request = Request("12.0.1", ["12.0.1", "12.0.2-beta"], Advisory("(, 12.0.1]")) with { Policy = Policy("minor-") };
        var decision = OverrideSelector.Select(request);
        await Assert.That(decision.Version?.ToNormalizedString()).IsEqualTo("12.0.2-beta");
    }

    [Test]
    public async Task Select_WithADisabledPolicy_HasNoFix()
    {
        var request = Request("12.0.1", ["12.0.1", "12.0.3"], Advisory("(, 12.0.2]")) with { Policy = Policy("disable") };
        var decision = OverrideSelector.Select(request);
        await Assert.That(decision.Outcome).IsEqualTo(OverrideOutcome.NoFix);
        await Assert.That(decision.Reason).Contains("disables updates");
    }

    [Test]
    public async Task Select_APackageTheProjectReferences_IsBlocked()
    {
        var request = Request("12.0.1", ["12.0.1", "12.0.3"], Advisory("(, 12.0.2]")) with { IsDirectReference = true };
        var decision = OverrideSelector.Select(request);
        await Assert.That(decision.Outcome).IsEqualTo(OverrideOutcome.Blocked);
        await Assert.That(decision.Reason).Contains("references it directly");
    }

    [Test]
    public async Task Select_ACentralPinAboveTheResolvedVersion_IsPromotedWithNoVersionOfItsOwn()
    {
        var request = Request("12.0.1", ["12.0.1", "12.0.3"], Advisory("(, 12.0.2]")) with { CentralPin = NuGetVersion.Parse("12.0.3") };
        var decision = OverrideSelector.Select(request);
        await Assert.That(decision.Outcome).IsEqualTo(OverrideOutcome.Promote);
        await Assert.That(decision.Version).IsNull();
    }

    [Test]
    public async Task Select_ACentralPinBelowTheResolvedVersion_IsBlocked()
    {
        var request = Request("12.0.2", ["12.0.1", "12.0.2", "12.0.3"], Advisory("(, 12.0.2]")) with
        {
            CentralPin = NuGetVersion.Parse("12.0.1"),
        };

        var decision = OverrideSelector.Select(request);
        await Assert.That(decision.Outcome).IsEqualTo(OverrideOutcome.Blocked);
        await Assert.That(decision.Reason).Contains("downgrade");
    }

    [Test]
    public async Task Select_AVulnerableCentralPin_IsBlocked()
    {
        var request = Request("12.0.1", ["12.0.1", "12.0.3"], Advisory("(, 12.0.2]")) with { CentralPin = NuGetVersion.Parse("12.0.2") };
        var decision = OverrideSelector.Select(request);
        await Assert.That(decision.Outcome).IsEqualTo(OverrideOutcome.Blocked);
        await Assert.That(decision.Reason).Contains("advisory covers");
    }

    // The sources' data and restore's verdict come from the same place, so this means the two disagree.
    [Test]
    public async Task Select_WhenNoAdvisoryCoversTheResolvedVersion_HasNoFix()
    {
        var decision = OverrideSelector.Select(Request("13.0.1", ["13.0.1"], Advisory("(, 12.0.2]")));
        await Assert.That(decision.Outcome).IsEqualTo(OverrideOutcome.NoFix);
        await Assert.That(decision.Reason).Contains("no advisory");
    }

    private static OverrideRequest Request(string resolved, string[] candidates, params PackageAdvisory[] advisories)
        => new()
        {
            ResolvedVersion = NuGetVersion.Parse(resolved),
            Candidates = [.. candidates.Select(NuGetVersion.Parse)],
            Advisories = advisories,
            AuditLevel = PackageVulnerabilitySeverity.Low,
            Policy = Policy("major"),
        };

    private static PackageAdvisory Advisory(string affected, PackageVulnerabilitySeverity severity = PackageVulnerabilitySeverity.High)
        => new(new Uri("https://example.invalid/advisory"), severity, VersionRange.Parse(affected));

    private static PackageUpdatePolicy Policy(string text)
    {
        _ = PackageUpdatePolicy.TryParse(text, out var policy);
        return policy;
    }
}
