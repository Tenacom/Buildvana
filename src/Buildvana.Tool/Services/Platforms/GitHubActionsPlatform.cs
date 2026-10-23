// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using Buildvana.Core;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Git;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Platforms;

/// <summary>
/// The GitHub Actions platform.
/// </summary>
internal sealed class GitHubActionsPlatform : CIPlatform
{
    private const string StepOutputsVariable = "GITHUB_OUTPUT";

    private readonly string? _stepOutputsPath;

    private GitHubActionsPlatform(string? stepOutputsPath, Uri? serverUrl)
    {
        _stepOutputsPath = stepOutputsPath;
        ServerUrl = serverUrl;
    }

    /// <inheritdoc/>
    public override string Name => "GitHub Actions";

    /// <inheritdoc/>
    /// <value>Always <see langword="true"/>.</value>
    public override bool IsCI => true;

    /// <inheritdoc/>
    public override GitIdentity? BotIdentity { get; } = new(
        "github-actions[bot]",
        "41898282+github-actions[bot]@users.noreply.github.com");

    /// <inheritdoc/>
    /// <value>The value of <c>GITHUB_SERVER_URL</c>.</value>
    public override Uri? ServerUrl { get; }

    /// <inheritdoc/>
    /// <value>Always <see cref="HostKind.GitHub"/>.</value>
    public override HostKind? ServerKind => HostKind.GitHub;

    /// <inheritdoc/>
    /// <value>Always <see langword="true"/>.</value>
    public override bool HasStepOutputs => true;

    /// <summary>
    /// Creates and returns an instance of <see cref="GitHubActionsPlatform"/> if <c>bv</c> runs in a GitHub Actions runner.
    /// </summary>
    /// <param name="getEnvironmentVariable">A function that returns the value of an environment variable,
    /// or <see langword="null"/> when the variable is not set.</param>
    /// <returns>If <c>GITHUB_ACTIONS</c> is <c>true</c>, in any case, a newly-created <see cref="GitHubActionsPlatform"/>;
    /// otherwise, <see langword="null"/>.</returns>
    public static CIPlatform? DetectIfApplicable(Func<string, string?> getEnvironmentVariable)
    {
        Guard.IsNotNull(getEnvironmentVariable);
        return string.Equals(getEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase)
            ? new GitHubActionsPlatform(
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
