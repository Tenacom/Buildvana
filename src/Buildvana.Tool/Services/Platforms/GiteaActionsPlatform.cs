// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using Buildvana.Core;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Git;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Platforms;

/// <summary>
/// The Gitea Actions platform.
/// </summary>
/// <remarks>
/// <para>A Gitea runner sets no <c>GITEA_SERVER_URL</c>, so the server comes from <c>GITHUB_SERVER_URL</c>.
/// The step outputs go to <c>GITHUB_OUTPUT</c>, which every Gitea runner sets.</para>
/// </remarks>
internal sealed class GiteaActionsPlatform : CIPlatform
{
    private const string StepOutputsVariable = "GITHUB_OUTPUT";

    private readonly string? _stepOutputsPath;

    private GiteaActionsPlatform(string? stepOutputsPath, Uri? serverUrl)
    {
        _stepOutputsPath = stepOutputsPath;
        BotIdentity = new("gitea-actions", $"gitea-actions@noreply.{serverUrl?.Host}");
        ServerUrl = serverUrl;
    }

    /// <inheritdoc/>
    public override string Name => "Gitea Actions";

    /// <inheritdoc/>
    /// <value>Always <see langword="true"/>.</value>
    public override bool IsCI => true;

    /// <inheritdoc/>
    public override GitIdentity? BotIdentity { get; }

    /// <inheritdoc/>
    /// <value>The value of <c>GITHUB_SERVER_URL</c>.</value>
    public override Uri? ServerUrl { get; }

    /// <inheritdoc/>
    /// <value>Always <see cref="HostKind.Gitea"/>.</value>
    public override HostKind? ServerKind => HostKind.Gitea;

    /// <inheritdoc/>
    /// <value>Always <see langword="true"/>.</value>
    public override bool HasStepOutputs => true;

    /// <summary>
    /// Creates and returns an instance of <see cref="GiteaActionsPlatform"/> if <c>bv</c> runs in a Gitea Actions runner.
    /// </summary>
    /// <param name="getEnvironmentVariable">A function that returns the value of an environment variable,
    /// or <see langword="null"/> when the variable is not set.</param>
    /// <returns>If <c>GITEA_ACTIONS</c> is <c>true</c>, in any case, a newly-created <see cref="GiteaActionsPlatform"/>;
    /// otherwise, <see langword="null"/>.</returns>
    public static CIPlatform? DetectIfApplicable(Func<string, string?> getEnvironmentVariable)
    {
        Guard.IsNotNull(getEnvironmentVariable);
        return string.Equals(getEnvironmentVariable("GITEA_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase)
            ? new GiteaActionsPlatform(
                getEnvironmentVariable(StepOutputsVariable),
                ParseServerUrl(getEnvironmentVariable("GITHUB_SERVER_URL")))
            : null;
    }

    /// <inheritdoc/>
    public override StepOutputWriter OpenStepOutputs()
        => _stepOutputsPath is { Length: > 0 } path
            ? new GitHubActionsStepOutputWriter(path)
            : throw new BuildFailedException($"Required environment variable {StepOutputsVariable} is missing or empty.");
}
