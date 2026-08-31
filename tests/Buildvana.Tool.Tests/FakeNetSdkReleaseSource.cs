// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Services.Dependencies;
using NuGet.Versioning;

/// <summary>
/// A .NET SDK release source scripted with the releases it knows.
/// </summary>
internal sealed class FakeNetSdkReleaseSource : INetSdkReleaseSource
{
    private readonly List<NetSdkRelease> _releases = [];

    /// <summary>
    /// Scripts releases of a channel.
    /// </summary>
    /// <param name="isLts">Whether the channel is long-term support.</param>
    /// <param name="versions">The SDK versions of the channel.</param>
    /// <returns>This instance, for chaining.</returns>
    public FakeNetSdkReleaseSource Knows(bool isLts, params string[] versions)
    {
        _releases.AddRange(versions.Select(version => new NetSdkRelease(NuGetVersion.Parse(version), isLts)));
        return this;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<NetSdkRelease>> GetReleasesAsync(
        NuGetVersion pinnedVersion,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<NetSdkRelease>>(_releases);
}
