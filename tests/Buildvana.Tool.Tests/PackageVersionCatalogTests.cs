// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Services.Dependencies;
using NuGet.Versioning;

internal sealed class PackageVersionCatalogTests
{
    [Test]
    public async Task Empty_KnowsNothing()
    {
        await Assert.That(PackageVersionCatalog.Empty.Knows(NuGetVersion.Parse("1.0.0"))).IsFalse();
    }

    [Test]
    public async Task Knows_ComparesByPrecedenceNotByText()
    {
        var catalog = NewCatalog(listed: ["13.0.0"], unlisted: []);
        await Assert.That(catalog.Knows(NuGetVersion.Parse("13.0"))).IsTrue();
        await Assert.That(catalog.IsListed(NuGetVersion.Parse("13.0"))).IsTrue();
    }

    [Test]
    public async Task ADelistedVersion_IsKnownAndNotListed()
    {
        var catalog = NewCatalog(listed: ["1.0.0"], unlisted: ["1.1.0"]);
        await Assert.That(catalog.Knows(NuGetVersion.Parse("1.1.0"))).IsTrue();
        await Assert.That(catalog.IsListed(NuGetVersion.Parse("1.1.0"))).IsFalse();
    }

    [Test]
    public async Task AVersionNoSourceHas_IsNeitherKnownNorListed()
    {
        var catalog = NewCatalog(listed: ["1.0.0"], unlisted: ["1.1.0"]);
        await Assert.That(catalog.Knows(NuGetVersion.Parse("2.0.0"))).IsFalse();
        await Assert.That(catalog.IsListed(NuGetVersion.Parse("2.0.0"))).IsFalse();
    }

    private static PackageVersionCatalog NewCatalog(string[] listed, string[] unlisted)
        => new()
        {
            Listed = [.. listed.Select(NuGetVersion.Parse)],
            Unlisted = [.. unlisted.Select(NuGetVersion.Parse)],
        };
}
