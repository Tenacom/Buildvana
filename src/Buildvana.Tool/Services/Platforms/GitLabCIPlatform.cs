// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Git;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Platforms;

/// <summary>
/// The GitLab CI platform.
/// </summary>
internal sealed class GitLabCIPlatform : CIPlatform
{
    private GitLabCIPlatform(string? serverHost, Uri? serverUrl)
    {
        BotIdentity = new("GitLab CI", $"gitlab-ci@noreply.{serverHost}");
        ServerUrl = serverUrl;
    }

    /// <inheritdoc/>
    public override string Name => "GitLab CI";

    /// <inheritdoc/>
    /// <value>Always <see langword="true"/>.</value>
    public override bool IsCI => true;

    /// <inheritdoc/>
    public override GitIdentity? BotIdentity { get; }

    /// <inheritdoc/>
    /// <value>The value of <c>CI_SERVER_URL</c>.</value>
    public override Uri? ServerUrl { get; }

    /// <inheritdoc/>
    /// <value>Always <see cref="HostKind.GitLab"/>.</value>
    public override HostKind? ServerKind => HostKind.GitLab;

    /// <summary>
    /// Creates and returns an instance of <see cref="GitLabCIPlatform"/> if <c>bv</c> runs in a GitLab CI runner.
    /// </summary>
    /// <param name="getEnvironmentVariable">A function that returns the value of an environment variable,
    /// or <see langword="null"/> when the variable is not set.</param>
    /// <returns>If <c>GITLAB_CI</c> is set, whatever its value, a newly-created <see cref="GitLabCIPlatform"/>;
    /// otherwise, <see langword="null"/>.</returns>
    public static CIPlatform? DetectIfApplicable(Func<string, string?> getEnvironmentVariable)
    {
        Guard.IsNotNull(getEnvironmentVariable);
        return getEnvironmentVariable("GITLAB_CI") is not null
            ? new GitLabCIPlatform(getEnvironmentVariable("CI_SERVER_HOST"), ParseServerUrl(getEnvironmentVariable("CI_SERVER_URL")))
            : null;
    }
}
