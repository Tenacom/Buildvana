// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Buildvana.Core.Versioning;
using NuGet.Versioning;

internal sealed class VersionCalculatorTests
{
    [Test]
    public async Task Compute_StableVersion_BuildsMajorMinorHeight()
    {
        var result = Compute(new VersionFileData(2, 1, false), height: 7, branchName: "main");
        await Assert.That(result.CurrentStr).IsEqualTo("2.1.7");
        await Assert.That(result.SemanticVersion).IsEqualTo(new SemanticVersion(2, 1, 7));
        await Assert.That(result.IsPrerelease).IsFalse();
    }

    [Test]
    public async Task Compute_PrereleaseVersion_AppendsTagToHeight()
    {
        var result = Compute(new VersionFileData(2, 0, true), height: 3, prereleaseTag: "preview");
        await Assert.That(result.CurrentStr).IsEqualTo("2.0.3-preview");
        await Assert.That(result.IsPrerelease).IsTrue();
    }

    [Test]
    public async Task Compute_PrereleaseWithoutTag_Throws()
    {
        await Assert.That(() => Compute(new VersionFileData(1, 0, true))).Throws<ArgumentException>();
    }

    [Test]
    public async Task Compute_NegativeHeight_Throws()
    {
        await Assert.That(() => Compute(new VersionFileData(1, 0, false), height: -1))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Compute_BranchMatchesAnyPattern_IsPublicReleaseTrue()
    {
        IReadOnlyList<Regex> branches = [new Regex("^main$"), new Regex(@"^v\d+\.\d+$")];
        var result = Compute(new VersionFileData(2, 0, false), branchName: "v2.0", publicReleaseBranches: branches);
        await Assert.That(result.IsPublicRelease).IsTrue();
    }

    [Test]
    public async Task Compute_BranchMatchesNoPattern_IsPublicReleaseFalse()
    {
        IReadOnlyList<Regex> branches = [new Regex("^main$")];
        var result = Compute(new VersionFileData(2, 0, false), branchName: "topic", publicReleaseBranches: branches);
        await Assert.That(result.IsPublicRelease).IsFalse();
    }

    [Test]
    public async Task Compute_EmptyBranchList_IsPublicReleaseFalse()
    {
        var result = Compute(new VersionFileData(2, 0, false), branchName: "main");
        await Assert.That(result.IsPublicRelease).IsFalse();
    }

    [Test]
    public async Task Compute_DetachedHead_IsPublicReleaseFalse()
    {
        IReadOnlyList<Regex> branches = [new Regex("^main$")];
        var result = Compute(
            new VersionFileData(2, 0, false),
            branchName: string.Empty,
            publicReleaseBranches: branches);
        await Assert.That(result.IsPublicRelease).IsFalse();
    }

    private static CalculatedVersion Compute(
        VersionFileData fileData,
        int height = 0,
        string branchName = "",
        IReadOnlyList<Regex>? publicReleaseBranches = null,
        string? prereleaseTag = null)
        => VersionCalculator.Compute(fileData, height, branchName, publicReleaseBranches ?? [], prereleaseTag);
}
