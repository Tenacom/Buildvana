// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Services.Hosts;

/// <summary>
/// A script fake for <see cref="ReleaseRemoteResolver"/>: it names one remote, whatever its URL.
/// </summary>
/// <param name="remote">The name of the remote to return.</param>
/// <remarks>
/// <para>A remote that a test can push to is a bare repository on disk, whose URL is a path, and a path never
/// matches the URL of a repository on a server.</para>
/// </remarks>
internal sealed class FakeReleaseRemoteResolver(string remote) : ReleaseRemoteResolver
{
    /// <inheritdoc/>
    public override string Resolve() => remote;
}
