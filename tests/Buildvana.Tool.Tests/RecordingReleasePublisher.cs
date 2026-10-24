// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Services.Hosts;
using Buildvana.Tool.Services.Release;

/// <summary>
/// A capture-and-script fake for <see cref="ReleasePublisher"/>: it records what a release asks of the host,
/// and creates nothing.
/// </summary>
internal sealed class RecordingReleasePublisher : ReleasePublisher
{
    private readonly List<ReleaseAsset> _publishedAssets = [];

    /// <summary>
    /// Gets a value indicating whether the release began.
    /// </summary>
    public bool IsBegun { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the release was published.
    /// </summary>
    public bool IsPublished { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the release was undone.
    /// </summary>
    public bool IsUndone { get; private set; }

    /// <summary>
    /// Gets the tag the release was published on, or <see langword="null"/> until it is published.
    /// </summary>
    public string? PublishedTag { get; private set; }

    /// <summary>
    /// Gets the SHA the release was published at, or <see langword="null"/> until it is published.
    /// </summary>
    public string? PublishedSha { get; private set; }

    /// <summary>
    /// Gets the assets the release was published with, in the order they were added.
    /// Empty until the release is published.
    /// </summary>
    public IReadOnlyList<ReleaseAsset> PublishedAssets => _publishedAssets;

    /// <summary>
    /// Gets the assets the release had checked, or <see langword="null"/> until they are checked.
    /// </summary>
    public IReadOnlyList<ReleaseAsset>? CheckedAssets { get; private set; }

    /// <summary>
    /// Gets a callback invoked while publishing, after the assets have been recorded, simulating the
    /// host's behavior. Exceptions thrown by the callback propagate to the caller, simulating a failed
    /// publication.
    /// </summary>
    public Action? OnPublishing { get; init; }

    /// <inheritdoc/>
    public override Task BeginAsync()
    {
        IsBegun = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override void CheckAssets(IReadOnlyList<ReleaseAsset> assets) => CheckedAssets = [.. assets];

    /// <inheritdoc/>
    public override Task PublishAsync(string tag, string sha, IReadOnlyList<ReleaseAsset> assets)
    {
        _publishedAssets.AddRange(assets);
        OnPublishing?.Invoke();
        PublishedTag = tag;
        PublishedSha = sha;
        IsPublished = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UndoAsync()
    {
        IsUndone = true;
        return Task.CompletedTask;
    }
}
