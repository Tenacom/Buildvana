// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

// The PackageReference items Sdk.targets adds from the BV_PackageReference and BV_PackageVersion items the modules
// declare, evaluated through the real Sdk.props, BeforeNETSdk.targets, and Sdk.targets in the stub layout of
// SdkPropsFixture. The modules directory of the layout is empty, and PackageVersions.props is a stub, so the project
// body declares the two items itself.
internal sealed class PackageInjectionTests
{
    private const string InjectedPackage = """
        <BV_PackageReference Include="Some.Analyzers" PrivateAssets="All" />
        <BV_PackageVersion Include="Some.Analyzers" Version="1.0.0" />
        """;

    [Test]
    public async Task Evaluate_InjectedPackage_AddsAnImplicitlyDefinedReferenceAtThePinnedVersion()
    {
        using var fixture = new SdkPropsFixture();
        fixture.WriteFile(".git/HEAD");
        var result = fixture.EvaluateNetSdkProject(string.Empty, InjectedPackage);
        await Assert.That(result.Errors).IsEmpty();
        var reference = result.PackageReferences.Single(static item => item.Id == "Some.Analyzers");
        await Assert.That(reference.Version).IsEqualTo("1.0.0");
        await Assert.That(reference.IsImplicitlyDefined).IsEqualTo("true");
    }

    [Test]
    public async Task Evaluate_InjectedPackageUnderCentralManagement_AddsAnImplicitlyDefinedReferenceAtThePinnedVersion()
    {
        using var fixture = new SdkPropsFixture();
        fixture.WriteFile(".git/HEAD");
        const string properties = "<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>";
        var result = fixture.EvaluateNetSdkProject(properties, InjectedPackage);
        await Assert.That(result.Errors).IsEmpty();
        var reference = result.PackageReferences.Single(static item => item.Id == "Some.Analyzers");
        await Assert.That(reference.Version).IsEqualTo("1.0.0");
        await Assert.That(reference.IsImplicitlyDefined).IsEqualTo("true");
    }

    // NuGet rejects a PackageVersion item for an implicitly defined reference (NU1009), so the reference must be
    // an ordinary one, and the PackageVersion item alone states its version.
    [Test]
    public async Task Evaluate_InjectedPackageWithAPackageVersion_AddsAnOrdinaryReferenceWithoutVersion()
    {
        using var fixture = new SdkPropsFixture();
        fixture.WriteFile(".git/HEAD");
        const string properties = "<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>";
        const string items = InjectedPackage + """<PackageVersion Include="Some.Analyzers" Version="2.0.0" />""";
        var result = fixture.EvaluateNetSdkProject(properties, items);
        await Assert.That(result.Errors).IsEmpty();
        var reference = result.PackageReferences.Single(static item => item.Id == "Some.Analyzers");
        await Assert.That(reference.Version).IsEmpty();
        await Assert.That(reference.IsImplicitlyDefined).IsNotEqualTo("true");
    }

    // Without central package management, a PackageVersion item is an ordinary item that NuGet ignores.
    [Test]
    public async Task Evaluate_InjectedPackageWithAPackageVersionAndNoCentralManagement_KeepsThePinnedVersion()
    {
        using var fixture = new SdkPropsFixture();
        fixture.WriteFile(".git/HEAD");
        const string items = InjectedPackage + """<PackageVersion Include="Some.Analyzers" Version="2.0.0" />""";
        var result = fixture.EvaluateNetSdkProject(string.Empty, items);
        await Assert.That(result.Errors).IsEmpty();
        var reference = result.PackageReferences.Single(static item => item.Id == "Some.Analyzers");
        await Assert.That(reference.Version).IsEqualTo("1.0.0");
        await Assert.That(reference.IsImplicitlyDefined).IsEqualTo("true");
    }
}
