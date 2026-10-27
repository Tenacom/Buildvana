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

    // A Gitea runner sets GITHUB_ACTIONS to true, which the GitHub Actions detector would match too.
    [Test]
    [Arguments("true")]
    [Arguments("TRUE")]
    public async Task Detect_WithGiteaActionsTrue_ReturnsGiteaActionsPlatform(string value)
    {
        var platform = Detect(
            ("GITEA_ACTIONS", value),
            ("GITHUB_ACTIONS", "true"),
            ("CI", "true"),
            ("GITHUB_SERVER_URL", "https://gitea.example.invalid"));

        await Assert.That(platform).IsTypeOf<GiteaActionsPlatform>();
        await Assert.That(platform.IsCI).IsTrue();
        await Assert.That(platform.BotIdentity!.Name).IsEqualTo("gitea-actions");
        await Assert.That(platform.BotIdentity!.Email).IsEqualTo("gitea-actions@noreply.gitea.example.invalid");
        await Assert.That(platform.HasStepOutputs).IsTrue();
    }

    // Forgejo runners v7 to v12 set GITEA_ACTIONS to true, which the Gitea Actions detector would match too.
    [Test]
    [Arguments("true")]
    [Arguments("TRUE")]
    public async Task Detect_WithForgejoActionsTrue_ReturnsForgejoActionsPlatform(string value)
    {
        var platform = Detect(("FORGEJO_ACTIONS", value), ("GITEA_ACTIONS", "true"), ("GITHUB_ACTIONS", "true"), ("CI", "true"));

        await Assert.That(platform).IsTypeOf<ForgejoActionsPlatform>();
        await Assert.That(platform.IsCI).IsTrue();
        await Assert.That(platform.BotIdentity!.Name).IsEqualTo("forgejo-actions");
        await Assert.That(platform.BotIdentity!.Email).IsEqualTo("noreply@forgejo.org");
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
    [Arguments("GITHUB_ACTIONS", "GITHUB_OUTPUT")]
    [Arguments("GITEA_ACTIONS", "GITHUB_OUTPUT")]
    [Arguments("FORGEJO_ACTIONS", "FORGEJO_OUTPUT")]
    public async Task OpenStepOutputs_WithOutputFile_WritesToTheFile(string marker, string outputVariable)
    {
        using var home = new TempHome();
        var path = home.GetFullPath("output.txt");
        var platform = Detect((marker, "true"), (outputVariable, path));

        platform.OpenStepOutputs().Write("version", "1.2.3");

        await Assert.That(await File.ReadAllTextAsync(path).ConfigureAwait(false)).IsEqualTo("version=1.2.3" + Environment.NewLine);
    }

    [Test]
    [Arguments("GITHUB_ACTIONS", "GITHUB_OUTPUT", null)]
    [Arguments("GITHUB_ACTIONS", "GITHUB_OUTPUT", "")]
    [Arguments("GITEA_ACTIONS", "GITHUB_OUTPUT", null)]
    [Arguments("GITEA_ACTIONS", "GITHUB_OUTPUT", "")]
    [Arguments("FORGEJO_ACTIONS", "FORGEJO_OUTPUT", null)]
    [Arguments("FORGEJO_ACTIONS", "FORGEJO_OUTPUT", "")]
    public async Task OpenStepOutputs_WithoutOutputFile_Fails(string marker, string outputVariable, string? path)
    {
        var platform = Detect((marker, "true"), (outputVariable, path));

        await Assert.That(platform.OpenStepOutputs)
            .Throws<BuildFailedException>()
            .WithMessageContaining(outputVariable, StringComparison.Ordinal);
    }

    // Forgejo runners before v9.0.0 set GITHUB_OUTPUT and no FORGEJO_OUTPUT.
    [Test]
    public async Task OpenStepOutputs_OnForgejoActionsWithGitHubOutputOnly_Fails()
    {
        using var home = new TempHome();
        var platform = Detect(("FORGEJO_ACTIONS", "true"), ("GITHUB_OUTPUT", home.GetFullPath("output.txt")));

        await Assert.That(platform.OpenStepOutputs)
            .Throws<BuildFailedException>()
            .WithMessageContaining("FORGEJO_OUTPUT", StringComparison.Ordinal);
    }

    [Test]
    public async Task OpenStepOutputs_OnPlatformWithoutStepOutputs_Throws()
    {
        var platform = Detect(("CI", "true"));

        await Assert.That(platform.OpenStepOutputs).Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments("GITHUB_ACTIONS", "GITHUB_SERVER_URL", HostKind.GitHub)]
    [Arguments("GITLAB_CI", "CI_SERVER_URL", HostKind.GitLab)]
    [Arguments("GITEA_ACTIONS", "GITHUB_SERVER_URL", HostKind.Gitea)]
    [Arguments("FORGEJO_ACTIONS", "FORGEJO_SERVER_URL", HostKind.Forgejo)]
    public async Task Detect_OnAPlatformWithAServer_TakesTheServerFromItsVariable(
        string marker,
        string serverVariable,
        HostKind kind)
    {
        var platform = Detect((marker, "true"), (serverVariable, "https://git.example.invalid"));

        await Assert.That(platform.ServerUrl!.Host).IsEqualTo("git.example.invalid");
        await Assert.That(platform.ServerKind).IsEqualTo(kind);
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
