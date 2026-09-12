// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

// The import point Sdk.props adds through BeforeMicrosoftNETSdkTargets, evaluated through the real Sdk.props,
// BeforeNETSdk.targets, and Sdk.targets in the stub layout of SdkPropsFixture. A Microsoft.NET.Sdk project reaches
// BeforeNETSdk.targets, which computes the project type. A project with no SDK never does, so Sdk.targets reports
// BVSDK1006. The .NET CLI sets FileBasedProgram in the project it generates for a file-based app, and the project
// body of the fixture stands in for it.
internal sealed class BeforeNetSdkTargetsTests
{
    [Test]
    [Arguments("", "BV_IsLibraryProject")]
    [Arguments("<OutputType>Exe</OutputType>", "BV_IsExeProject")]
    [Arguments("<OutputType>WinExe</OutputType>", "BV_IsExeProject")]
    [Arguments("<IsTestingPlatformApplication>true</IsTestingPlatformApplication>", "BV_IsTestProject")]
    [Arguments(
        "<UsingMicrosoftNoTargetsSdk>true</UsingMicrosoftNoTargetsSdk><IsTestingPlatformApplication>true</IsTestingPlatformApplication>",
        "BV_IsNoTargetsProject")]
    [Arguments("<FileBasedProgram>true</FileBasedProgram><OutputType>Exe</OutputType>", "BV_IsFileBasedAppProject")]
    [Arguments(
        "<FileBasedProgram>true</FileBasedProgram><IsTestingPlatformApplication>true</IsTestingPlatformApplication>",
        "BV_IsFileBasedAppProject")]
    public async Task Evaluate_NetSdkProject_ComputesTheProjectType(string properties, string expected)
    {
        using var fixture = new SdkPropsFixture();
        fixture.WriteFile(".git/HEAD");
        var result = fixture.EvaluateNetSdkProject(properties);
        await Assert.That(result.Errors).IsEmpty();
        await Assert.That(TrueProjectTypes(result)).IsEqualTo(expected);
    }

    [Test]
    public async Task Evaluate_ProjectWithoutSdk_ReportsBVSDK1006()
    {
        using var fixture = new SdkPropsFixture();
        fixture.WriteFile(".git/HEAD");
        var result = fixture.EvaluateProjectWithoutSdk("<OutputType>Library</OutputType>");
        await Assert.That(result.Errors.Select(static error => error.Code)).IsEquivalentTo(["BVSDK1006"]);
        await Assert.That(TrueProjectTypes(result)).IsEmpty();
    }

    private static string TrueProjectTypes(SdkEvaluationResult result)
        => string.Join(", ", result.ProjectType.Where(static pair => pair.Value == "true").Select(static pair => pair.Key));
}
