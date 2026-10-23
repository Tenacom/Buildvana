// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.Testing;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Platforms;

internal sealed class CIPlatformTests
{
    [Test]
    public async Task Detect_WithEmptyEnvironment_ReturnsLocalPlatform()
    {
        var platform = Detect();

        await Assert.That(platform).IsTypeOf<LocalPlatform>();
        await Assert.That(platform.IsCI).IsFalse();
        await Assert.That(platform.BotIdentity).IsNull();
        await Assert.That(platform.HasStepOutputs).IsFalse();
    }

    [Test]
    [Arguments("true")]
    [Arguments("TRUE")]
    public async Task Detect_WithGitHubActionsTrue_ReturnsGitHubActionsPlatform(string value)
    {
        var platform = Detect(("GITHUB_ACTIONS", value), ("CI", "true"));

        await Assert.That(platform).IsTypeOf<GitHubActionsPlatform>();
        await Assert.That(platform.IsCI).IsTrue();
        await Assert.That(platform.BotIdentity!.Name).IsEqualTo("github-actions[bot]");
        await Assert.That(platform.HasStepOutputs).IsTrue();
    }

    // GitLab CI sets CI to true, which the generic detector would match too.
    [Test]
    [Arguments("true")]
    [Arguments("")]
    public async Task Detect_WithGitLabCISet_ReturnsGitLabCIPlatform(string value)
    {
        var platform = Detect(("GITLAB_CI", value), ("CI", "true"), ("CI_SERVER_HOST", "gitlab.example.invalid"));

        await Assert.That(platform).IsTypeOf<GitLabCIPlatform>();
        await Assert.That(platform.IsCI).IsTrue();
        await Assert.That(platform.BotIdentity!.Email).IsEqualTo("gitlab-ci@noreply.gitlab.example.invalid");
        await Assert.That(platform.HasStepOutputs).IsFalse();
    }

    [Test]
    public async Task Detect_WithGitLabCIAndGitHubActions_ReturnsGitLabCIPlatform()
    {
        var platform = Detect(("GITLAB_CI", "true"), ("GITHUB_ACTIONS", "true"));

        await Assert.That(platform).IsTypeOf<GitLabCIPlatform>();
    }

    [Test]
    [Arguments("CI", "true")]
    [Arguments("CI", "1")]
    [Arguments("CI", "Yes")]
    [Arguments("CI", "ON")]
    [Arguments("TF_BUILD", "True")]
    [Arguments("GITHUB_ACTIONS", "1")]
    [Arguments("APPVEYOR", "true")]
    [Arguments("TRAVIS", "true")]
    [Arguments("CIRCLECI", "true")]
    [Arguments("TEAMCITY_VERSION", "2025.1")]
    [Arguments("JB_SPACE_API_URL", "https://space.example.invalid")]
    public async Task Detect_WithOneCIMarker_ReturnsGenericCIPlatform(string name, string value)
    {
        var platform = Detect((name, value));

        await Assert.That(platform).IsTypeOf<GenericCIPlatform>();
        await Assert.That(platform.IsCI).IsTrue();
        await Assert.That(platform.BotIdentity).IsNull();
        await Assert.That(platform.HasStepOutputs).IsFalse();
    }

    [Test]
    [Arguments("CODEBUILD_BUILD_ID", "AWS_REGION")]
    [Arguments("BUILD_ID", "BUILD_URL")]
    [Arguments("BUILD_ID", "PROJECT_ID")]
    public async Task Detect_WithBothVariablesOfAPair_ReturnsGenericCIPlatform(string first, string second)
    {
        await Assert.That(Detect((first, "x"), (second, "y"))).IsTypeOf<GenericCIPlatform>();
        await Assert.That(Detect((first, "x"))).IsTypeOf<LocalPlatform>();
        await Assert.That(Detect((second, "y"))).IsTypeOf<LocalPlatform>();
    }

    // A value the .NET CLI does not read as true marks no CI runner, whatever the runner meant by it.
    [Test]
    [Arguments("false")]
    [Arguments("0")]
    [Arguments("")]
    [Arguments("woodpecker")]
    [Arguments(" true")]
    public async Task Detect_WithCINotTrue_ReturnsLocalPlatform(string value)
    {
        await Assert.That(Detect(("CI", value))).IsTypeOf<LocalPlatform>();
    }

    [Test]
    public async Task Detect_WithEmptyTeamCityVersion_ReturnsLocalPlatform()
    {
        await Assert.That(Detect(("TEAMCITY_VERSION", string.Empty))).IsTypeOf<LocalPlatform>();
    }

    [Test]
    public async Task OpenStepOutputs_OnGitHubActionsWithOutputFile_WritesToTheFile()
    {
        using var home = new TempHome();
        var path = home.GetFullPath("output.txt");
        var platform = Detect(("GITHUB_ACTIONS", "true"), ("GITHUB_OUTPUT", path));

        platform.OpenStepOutputs().Write("version", "1.2.3");

        await Assert.That(await File.ReadAllTextAsync(path).ConfigureAwait(false)).IsEqualTo("version=1.2.3" + Environment.NewLine);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    public async Task OpenStepOutputs_OnGitHubActionsWithoutOutputFile_Fails(string? path)
    {
        var platform = Detect(("GITHUB_ACTIONS", "true"), ("GITHUB_OUTPUT", path));

        var exception = await Assert.That(platform.OpenStepOutputs).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("GITHUB_OUTPUT");
    }

    [Test]
    public async Task OpenStepOutputs_OnPlatformWithoutStepOutputs_Throws()
    {
        var platform = Detect(("CI", "true"));

        await Assert.That(platform.OpenStepOutputs).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Detect_OnGitHubActions_TakesTheServerFromGitHubServerUrl()
    {
        var platform = Detect(("GITHUB_ACTIONS", "true"), ("GITHUB_SERVER_URL", "https://github.example.invalid"));

        await Assert.That(platform.ServerUrl!.Host).IsEqualTo("github.example.invalid");
        await Assert.That(platform.ServerKind).IsEqualTo(HostKind.GitHub);
    }

    [Test]
    public async Task Detect_OnGitLabCI_TakesTheServerFromCIServerUrl()
    {
        var platform = Detect(("GITLAB_CI", "true"), ("CI_SERVER_URL", "https://gitlab.example.invalid"));

        await Assert.That(platform.ServerUrl!.Host).IsEqualTo("gitlab.example.invalid");
        await Assert.That(platform.ServerKind).IsNull();
    }

    // A value that is not an absolute URL names no server, and host detection then has no hint to go on.
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("github.example.invalid")]
    public async Task Detect_OnGitHubActions_WithoutAnAbsoluteServerUrl_HasNoServerUrl(string? value)
    {
        var platform = Detect(("GITHUB_ACTIONS", "true"), ("GITHUB_SERVER_URL", value));

        await Assert.That(platform.ServerUrl).IsNull();
    }

    [Test]
    [Arguments("CI", "true")]
    [Arguments("TEAMCITY_VERSION", "2025.1")]
    public async Task Detect_OnOtherPlatforms_HasNoServer(string name, string value)
    {
        var platforms = new[] { Detect(), Detect((name, value)) };

        foreach (var platform in platforms)
        {
            await Assert.That(platform.ServerUrl).IsNull();
            await Assert.That(platform.ServerKind).IsNull();
        }
    }

    private static CIPlatform Detect(params (string Name, string? Value)[] variables)
    {
        var environment = variables.ToDictionary(x => x.Name, x => x.Value, StringComparer.Ordinal);
        return CIPlatform.Detect(environment.GetValueOrDefault);
    }
}
