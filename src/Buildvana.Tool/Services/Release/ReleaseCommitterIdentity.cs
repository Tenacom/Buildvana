// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Runtime;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.Platforms;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Release;

/// <summary>
/// Resolves the identity the commits of <c>bv release</c> are made with.
/// </summary>
internal static class ReleaseCommitterIdentity
{
    /// <summary>
    /// Resolves the committer identity of a release: the <c>git.identity</c> setting, then the bot identity of
    /// the CI platform, then the identity the Git configuration states.
    /// </summary>
    /// <param name="config">The Git configuration of <c>buildvana.jsonc</c>.</param>
    /// <param name="platform">The CI platform <c>bv</c> runs on.</param>
    /// <param name="gitConfigurationIdentity">The identity the Git configuration states, or <see langword="null"/>
    /// if it states none.</param>
    /// <returns>The first identity of the three that exists, with a description of where it comes from;
    /// or <see langword="null"/> if none exists.</returns>
    public static (GitIdentity Identity, string Source)? Resolve(
        GitConfig config,
        CIPlatform platform,
        GitIdentity? gitConfigurationIdentity)
    {
        Guard.IsNotNull(config);
        Guard.IsNotNull(platform);
        if (config.Identity is { } configured)
        {
            return (new GitIdentity(configured.Name, configured.Email), "git.identity");
        }

        if (platform.BotIdentity is { } bot)
        {
            return (bot, $"the bot identity of {platform.Name}");
        }

        return gitConfigurationIdentity is not null ? (gitConfigurationIdentity, "the Git configuration") : null;
    }
}
