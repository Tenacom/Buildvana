// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Buildvana.Tool.Services.Release;

namespace Buildvana.Tool.Services.Hosts;

/// <summary>
/// Base class for the host side of one release: what a <see cref="GitHost"/> creates on its server.
/// </summary>
/// <remarks>
/// <para>The release tag is not the publisher's. <see cref="ServerRelease"/> creates it, pushes it, and deletes it.</para>
/// </remarks>
internal abstract class ReleasePublisher
{
    private protected ReleasePublisher()
    {
    }

    /// <summary>
    /// Asynchronously prepares the release on the host, before any commit of the release exists.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    /// <remarks>
    /// <para>A host that can create something provisional creates it here, so that a token without the needed
    /// permissions fails the release while there is nothing to roll back.</para>
    /// </remarks>
    public abstract Task BeginAsync();

    /// <summary>
    /// Asynchronously publishes the release on a tag that already exists on the host.
    /// </summary>
    /// <param name="tag">The name of the release tag.</param>
    /// <param name="sha">The SHA of the commit the tag points to.</param>
    /// <param name="assets">The files to attach to the release.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public abstract Task PublishAsync(string tag, string sha, IReadOnlyList<ReleaseAsset> assets);

    /// <summary>
    /// Asynchronously removes from the host what <see cref="BeginAsync"/> and <see cref="PublishAsync"/> created.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public abstract Task UndoAsync();
}
