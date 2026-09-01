// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Services.Dependencies;
using NuGet.Protocol;
using NuGet.Versioning;

internal sealed class AdvisoryIndexTests
{
    [Test]
    public async Task For_AnIdWithAdvisories_HasThemAll()
    {
        var index = new AdvisoryIndex(
        [
            ("Newtonsoft.Json", Advisory("(, 12.0.3)")),
            ("Serilog", Advisory("(, 2.0.0)")),
            ("Newtonsoft.Json", Advisory("(, 13.0.1)")),
        ]);

        var ranges = index.For("Newtonsoft.Json").Select(static advisory => advisory.AffectedVersions.ToNormalizedString());
        await Assert.That(ranges).IsEquivalentTo(["(, 12.0.3)", "(, 13.0.1)"]);
    }

    // Package ids are case-insensitive, and the id in the vulnerability data is the one its author typed.
    [Test]
    public async Task For_MatchesAnIdWhateverItsCase()
    {
        var index = new AdvisoryIndex([("Newtonsoft.Json", Advisory("(, 12.0.3)"))]);
        await Assert.That(index.For("newtonsoft.json").Count).IsEqualTo(1);
    }

    [Test]
    public async Task For_AnIdWithNoAdvisory_IsEmpty()
    {
        var index = new AdvisoryIndex([("Serilog", Advisory("(, 2.0.0)"))]);
        await Assert.That(index.For("Newtonsoft.Json")).IsEmpty();
    }

    [Test]
    public async Task Empty_KnowsOfNoAdvisory()
        => await Assert.That(AdvisoryIndex.Empty.For("Newtonsoft.Json")).IsEmpty();

    private static PackageAdvisory Advisory(string affectedVersions)
        => new(new Uri("https://example.invalid/advisory"), PackageVulnerabilitySeverity.Moderate, VersionRange.Parse(affectedVersions));
}
