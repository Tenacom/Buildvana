// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.ServerAdapters;

/// <summary>
/// A script fake for <see cref="ServerAdapter"/>: every member answers with a constant, and creating a release
/// hands out a <see cref="RecordingServerRelease"/>.
/// </summary>
/// <param name="services">The service provider handed to the created release.</param>
/// <remarks>
/// <para>The fake deliberately carries no logic. <c>ServerAdapter</c> is to be split into a CI-platform adapter
/// and a Git-host adapter; the knobs below are grouped by the side each one will end up on, so that the split
/// can cut this class in two without rewriting what it answers.</para>
/// </remarks>
internal sealed class RecordingServerAdapter(IServiceProvider services) : ServerAdapter
{
    private const string RepositoryPath = "https://git.example.invalid/tenacom/test-repo";

    // --- Platform side ---

    /// <summary>
    /// Gets a value indicating whether the build is a cloud build (<see cref="IsCloudBuild"/>).
    /// </summary>
    public bool CloudBuild { get; init; } = true;

    /// <summary>
    /// Gets the identity the CI bot commits with (<see cref="CIBotIdentity"/>), or <see langword="null"/>
    /// for a platform that has none.
    /// </summary>
    public GitIdentity? BotIdentity { get; init; } = new("Buildvana Test Bot", "bot@buildvana.invalid");

    /// <summary>
    /// Gets the secret half of the push credentials (<see cref="PushPassword"/>), or <see langword="null"/>
    /// for a platform that provides none.
    /// </summary>
    public string? PushSecret { get; init; } = "test-token";

    // --- Host side ---

    /// <summary>
    /// Gets the username half of the push credentials (<see cref="PushUsername"/>), or <see langword="null"/>
    /// for a host with no username convention.
    /// </summary>
    public string? PushUser { get; init; } = "x-access-token";

    /// <summary>
    /// Gets the callback the created release publishes with (<see cref="RecordingServerRelease.OnPublishing"/>).
    /// </summary>
    public Action? OnPublishing { get; init; }

    /// <summary>
    /// Gets the release created by <see cref="CreateReleaseAsync"/>, or <see langword="null"/> if none was created.
    /// </summary>
    public RecordingServerRelease? Release { get; private set; }

    /// <inheritdoc/>
    public override string Name => "Test CI platform";

    /// <inheritdoc/>
    public override string HostName => "git.example.invalid";

    /// <inheritdoc/>
    public override string RepositoryOwner => "tenacom";

    /// <inheritdoc/>
    public override string RepositoryName => "test-repo";

    /// <inheritdoc/>
    public override Uri RepositoryUrl => new(RepositoryPath);

    /// <inheritdoc/>
    public override bool IsCloudBuild => CloudBuild;

    /// <inheritdoc/>
    public override GitIdentity? CIBotIdentity => BotIdentity;

    /// <inheritdoc/>
    public override string? PushUsername => PushUser;

    /// <inheritdoc/>
    public override string? PushPassword => PushSecret;

    /// <inheritdoc/>
    public override Task<bool> IsPrivateRepositoryAsync() => Task.FromResult(false);

    /// <inheritdoc/>
    public override Uri GetReleaseUrl(string version) => new($"{RepositoryPath}/releases/tag/{version}");

    /// <inheritdoc/>
    public override Uri GetFileUrl(string path, string commitish) => new($"{RepositoryPath}/blob/{commitish}/{path}");

    /// <inheritdoc/>
    public override Task<ServerRelease> CreateReleaseAsync()
    {
        Release = new RecordingServerRelease(services) { OnPublishing = OnPublishing };
        return Task.FromResult<ServerRelease>(Release);
    }
}
