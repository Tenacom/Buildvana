// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Runtime;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.Hosts;
using Buildvana.Tool.Services.Hosts.GitHub;

/// <summary>
/// A script fake for <see cref="GitHost"/>: every member answers with a constant, and creating a release
/// publisher hands out a <see cref="RecordingReleasePublisher"/>.
/// </summary>
/// <remarks>
/// <para>The URLs come from <see cref="GitHubRepositoryUrls"/>, so that a release over the fake sees the same
/// rejections a release on GitHub does: a file URL for a path outside the repository is an error, not a URL.</para>
/// </remarks>
internal sealed class RecordingHost : GitHost
{
    /// <summary>
    /// The URL of the repository the fake stands for.
    /// </summary>
    public const string Url = "https://git.example.invalid/tenacom/test-repo";

    private static readonly GitHubRepositoryUrls Urls = new("git.example.invalid", "tenacom", "test-repo");

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingHost"/> class.
    /// </summary>
    public RecordingHost()
        : base(ParseUrl())
    {
    }

    /// <summary>
    /// Gets the value of <see cref="TokenEnv"/>.
    /// </summary>
    public string? TokenVariable { get; init; }

    /// <summary>
    /// Gets the value of <see cref="PushUsername"/>.
    /// </summary>
    public string? PushUser { get; init; } = "x-access-token";

    /// <summary>
    /// Gets the callback the created publisher publishes with (<see cref="RecordingReleasePublisher.OnPublishing"/>).
    /// </summary>
    public Action? OnPublishing { get; init; }

    /// <summary>
    /// Gets the publisher created by <see cref="CreateReleasePublisher"/>, or <see langword="null"/> if none was created.
    /// </summary>
    public RecordingReleasePublisher? Publisher { get; private set; }

    /// <inheritdoc/>
    public override string Name => "Test host";

    /// <inheritdoc/>
    public override HostKind? Kind => HostKind.GitHub;

    /// <inheritdoc/>
    public override Uri RepositoryUrl => Urls.Repository;

    /// <inheritdoc/>
    public override string? TokenEnv => TokenVariable;

    /// <inheritdoc/>
    public override string? PushUsername => PushUser;

    /// <inheritdoc/>
    public override Uri GetReleaseUrl(string tag) => Urls.ReleaseTag(tag);

    /// <inheritdoc/>
    public override Uri GetFileUrl(string path, GitRef gitRef) => Urls.File(path, gitRef.Name);

    /// <inheritdoc/>
    public override Task<bool> IsPrivateRepositoryAsync() => Task.FromResult(false);

    /// <inheritdoc/>
    public override ReleasePublisher CreateReleasePublisher()
    {
        Publisher = new RecordingReleasePublisher { OnPublishing = OnPublishing };
        return Publisher;
    }

    private static GitUrlInfo ParseUrl() => GitUrlInfo.TryCreate(Url, out var info) ? info : throw new InvalidOperationException(Url);
}
