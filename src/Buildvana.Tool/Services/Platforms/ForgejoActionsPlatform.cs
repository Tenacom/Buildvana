// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using Buildvana.Core;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Git;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Platforms;

/// <summary>
/// The Forgejo Actions platform.
/// </summary>
/// <remarks>
/// <para>Forgejo runners set <c>FORGEJO_SERVER_URL</c> and <c>FORGEJO_OUTPUT</c> since v9.0.0. An older runner
/// fails at <see cref="OpenStepOutputs"/>, with a message that names <c>FORGEJO_OUTPUT</c>.</para>
/// </remarks>
internal sealed class ForgejoActionsPlatform : CIPlatform
{
    private const string StepOutputsVariable = "FORGEJO_OUTPUT";

    private readonly string? _stepOutputsPath;

    private ForgejoActionsPlatform(string? stepOutputsPath, Uri? serverUrl)
    {
        _stepOutputsPath = stepOutputsPath;
        ServerUrl = serverUrl;
    }

    /// <inheritdoc/>
    public override string Name => "Forgejo Actions";

    /// <inheritdoc/>
    /// <value>Always <see langword="true"/>.</value>
    public override bool IsCI => true;

    /// <inheritdoc/>
    public override GitIdentity? BotIdentity { get; } = new("forgejo-actions", "noreply@forgejo.org");

    /// <inheritdoc/>
    /// <value>The value of <c>FORGEJO_SERVER_URL</c>.</value>
    public override Uri? ServerUrl { get; }

    /// <inheritdoc/>
    /// <value>Always <see cref="HostKind.Forgejo"/>.</value>
    public override HostKind? ServerKind => HostKind.Forgejo;

    /// <inheritdoc/>
    /// <value>Always <see langword="true"/>.</value>
    public override bool HasStepOutputs => true;

    /// <summary>
    /// Creates and returns an instance of <see cref="ForgejoActionsPlatform"/> if <c>bv</c> runs in a Forgejo Actions runner.
    /// </summary>
    /// <param name="getEnvironmentVariable">A function that returns the value of an environment variable,
    /// or <see langword="null"/> when the variable is not set.</param>
    /// <returns>If <c>FORGEJO_ACTIONS</c> is <c>true</c>, in any case, a newly-created <see cref="ForgejoActionsPlatform"/>;
    /// otherwise, <see langword="null"/>.</returns>
    public static CIPlatform? DetectIfApplicable(Func<string, string?> getEnvironmentVariable)
    {
        Guard.IsNotNull(getEnvironmentVariable);
        return string.Equals(getEnvironmentVariable("FORGEJO_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase)
            ? new ForgejoActionsPlatform(
                getEnvironmentVariable(StepOutputsVariable),
                ParseServerUrl(getEnvironmentVariable("FORGEJO_SERVER_URL")))
            : null;
    }

    /// <inheritdoc/>
    public override StepOutputWriter OpenStepOutputs()
        => _stepOutputsPath is { Length: > 0 } path
            ? new GitHubActionsStepOutputWriter(path)
            : throw new BuildFailedException($"Required environment variable {StepOutputsVariable} is missing or empty.");
}
