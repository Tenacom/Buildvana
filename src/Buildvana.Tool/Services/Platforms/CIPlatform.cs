// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Git;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Platforms;

/// <summary>
/// Base class for the platforms <c>bv</c> runs on: a CI runner, or a machine with none.
/// </summary>
/// <remarks>
/// <para>A platform is detected from the environment alone. It never opens the Git repository and never reads
/// the configuration file, so that every command can depend on it, wherever it runs.</para>
/// </remarks>
internal abstract class CIPlatform
{
    // The order matters: each detector runs only when the ones above it found nothing. Forgejo runners v7 to v12
    // set GITEA_ACTIONS, and Gitea and Forgejo runners set GITHUB_ACTIONS. GenericCIPlatform recognizes every
    // runner the .NET CLI recognizes, the more specific ones included.
    private static readonly Func<Func<string, string?>, CIPlatform?>[] Detectors = [
        GitLabCIPlatform.DetectIfApplicable,
        ForgejoActionsPlatform.DetectIfApplicable,
        GiteaActionsPlatform.DetectIfApplicable,
        GitHubActionsPlatform.DetectIfApplicable,
        GenericCIPlatform.DetectIfApplicable,
    ];

    private protected CIPlatform()
    {
    }

    /// <summary>
    /// Gets the display name of the platform.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets a value indicating whether the platform is a CI runner.
    /// </summary>
    /// <value><see langword="true"/> on every platform but <see cref="LocalPlatform"/>.</value>
    public abstract bool IsCI { get; }

    /// <summary>
    /// Gets the identity the platform commits with, or <see langword="null"/> if the platform has none.
    /// </summary>
    public abstract GitIdentity? BotIdentity { get; }

    /// <summary>
    /// Gets the URL of the server that runs the platform, or <see langword="null"/> if the platform states none.
    /// </summary>
    public virtual Uri? ServerUrl => null;

    /// <summary>
    /// Gets the kind of Git host of the server that runs the platform, or <see langword="null"/> if the platform
    /// belongs to no host <c>bv</c> knows.
    /// </summary>
    /// <remarks>
    /// <para>A repository whose URL has the host name of <see cref="ServerUrl"/> is on a host of this kind.</para>
    /// </remarks>
    public virtual HostKind? ServerKind => null;

    /// <summary>
    /// Gets a value indicating whether the platform collects named outputs of a job step.
    /// </summary>
    /// <value>If <see cref="OpenStepOutputs"/> can be called, <see langword="true"/>;
    /// otherwise, <see langword="false"/>.</value>
    public virtual bool HasStepOutputs => false;

    /// <summary>
    /// Detects the platform <c>bv</c> runs on.
    /// </summary>
    /// <param name="getEnvironmentVariable">A function that returns the value of an environment variable,
    /// or <see langword="null"/> when the variable is not set.</param>
    /// <returns>The first platform whose detector recognizes the environment, or a <see cref="LocalPlatform"/>.</returns>
    public static CIPlatform Detect(Func<string, string?> getEnvironmentVariable)
    {
        Guard.IsNotNull(getEnvironmentVariable);
        foreach (var detector in Detectors)
        {
            if (detector(getEnvironmentVariable) is { } platform)
            {
                return platform;
            }
        }

        return new LocalPlatform();
    }

    /// <summary>
    /// Returns a writer of named step outputs.
    /// </summary>
    /// <returns>A newly-created <see cref="StepOutputWriter"/>.</returns>
    /// <exception cref="InvalidOperationException"><see cref="HasStepOutputs"/> is <see langword="false"/>.</exception>
    /// <exception cref="Core.BuildFailedException">The environment does not name the file that collects the outputs.</exception>
    /// <remarks>
    /// <para>Call this method before changing anything that needs undoing: a missing file then fails the
    /// operation while there is nothing to roll back.</para>
    /// </remarks>
    public virtual StepOutputWriter OpenStepOutputs()
        => ThrowHelper.ThrowInvalidOperationException<StepOutputWriter>($"Internal error: {Name} has no step outputs.");

    /// <summary>
    /// Parses the value of a variable that holds the URL of a server.
    /// </summary>
    /// <param name="value">The value of the variable, or <see langword="null"/> if it is not set.</param>
    /// <returns>The URL, or <see langword="null"/> if <paramref name="value"/> is not an absolute URL.</returns>
    private protected static Uri? ParseServerUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var url) ? url : null;
}
