// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

/// <summary>
/// The knobs of a <see cref="ReleaseHarness"/>: what the repository contains and how the configuration file,
/// the server adapter, and the hook are set up. Every one of them has the value a plain prerelease release
/// needs, so a test only names what its own scenario changes.
/// </summary>
internal sealed record ReleaseHarnessOptions
{
    /// <summary>
    /// Gets the content of the version file, without the trailing newline the harness adds.
    /// </summary>
    public string VersionSpec { get; init; } = "2.3-";

    /// <summary>
    /// Gets a value indicating whether the version file is committed. When <see langword="false"/> it is
    /// written after the initial commit and left untracked, so that no commit carries the version line.
    /// </summary>
    public bool CommitVersionFile { get; init; } = true;

    /// <summary>
    /// Gets the content of the changelog, or <see langword="null"/> for a repository without one.
    /// </summary>
    public string? Changelog { get; init; }

    /// <summary>
    /// Gets the value of <c>release.changelogUpdates</c>.
    /// </summary>
    public string ChangelogUpdates { get; init; } = "stable";

    /// <summary>
    /// Gets the value of <c>release.emptyChangelog</c>, or <see langword="null"/> to leave it unset.
    /// </summary>
    public string? EmptyChangelog { get; init; }

    /// <summary>
    /// Gets the value of <c>release.dogfood</c>.
    /// </summary>
    public bool Dogfood { get; init; } = true;

    /// <summary>
    /// Gets the value of <c>release.checkPublicApi</c>.
    /// </summary>
    public bool CheckPublicApi { get; init; } = true;

    /// <summary>
    /// Gets the content of the unshipped public API file, or <see langword="null"/> for a repository with
    /// no public API files. When set, a shipped file is written alongside it.
    /// </summary>
    public string? UnshippedPublicApi { get; init; }

    /// <summary>
    /// Gets a value indicating whether the repository has a <c>release/post-release</c> hook file.
    /// </summary>
    public bool WithHook { get; init; }

    /// <summary>
    /// Gets a value indicating whether the build is a cloud build.
    /// </summary>
    public bool CloudBuild { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the server adapter provides a CI bot identity.
    /// </summary>
    public bool WithBotIdentity { get; init; } = true;

    /// <summary>
    /// Gets a callback invoked while publishing the release. Throw from it to simulate a publication
    /// that fails after the repository has already been updated and pushed.
    /// </summary>
    public Action? OnPublishing { get; init; }
}
