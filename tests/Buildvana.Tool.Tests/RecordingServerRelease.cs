// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Services.ServerAdapters;

/// <summary>
/// A capture-and-script fake for <see cref="ServerRelease"/>. The base class is the real one, so the
/// repository-side operations (the release commit, post-release commits, and the push) actually happen;
/// only the host-side ones — asset upload and publication — are recorded instead of performed.
/// </summary>
internal sealed class RecordingServerRelease(IServiceProvider services) : ServerRelease(services)
{
    private readonly List<RecordedAsset> _publishedAssets = [];

    /// <summary>
    /// Gets the assets the release was published with, in the order they were added.
    /// Empty until the release is published.
    /// </summary>
    public IReadOnlyList<RecordedAsset> PublishedAssets => _publishedAssets;

    /// <summary>
    /// Gets a value indicating whether the release was published.
    /// </summary>
    public bool IsPublished { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the release's publication was rolled back.
    /// </summary>
    public bool IsPublishUndone { get; private set; }

    /// <summary>
    /// Gets a callback invoked while publishing, after the assets have been recorded, simulating the
    /// host's behavior. Exceptions thrown by the callback propagate to the caller, simulating a failed
    /// publication.
    /// </summary>
    public Action? OnPublishing { get; init; }

    /// <inheritdoc/>
    protected override Task DoPublishAsync(IReadOnlyList<AssetData> assets)
    {
        _publishedAssets.AddRange(assets.Select(x => new RecordedAsset(x.Path, x.Description, x.MimeType)));
        OnPublishing?.Invoke();
        IsPublished = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    protected override Task UndoPublishAsync()
    {
        IsPublishUndone = true;
        return Task.CompletedTask;
    }
}
