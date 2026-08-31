// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using CommunityToolkit.Diagnostics;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Reads the .NET SDK releases from the official .NET release index.
/// </summary>
/// <remarks>
/// <para>The index states one entry per channel, naming the channel's release type and the address of the
/// file that lists the channel's releases. The SDK versions themselves live in those per-channel files, so
/// reading a channel costs a request of its own, and only the channels a pinned version could move to are
/// read.</para>
/// <para>The index is the oracle for existence as well: a pinned version the channels do not state is a
/// version Microsoft never shipped, which the caller reports as the user error it is.</para>
/// </remarks>
internal sealed class DotNetReleaseIndex : INetSdkReleaseSource, IDisposable
{
    /// <summary>
    /// The address of the official .NET release index.
    /// </summary>
    public const string IndexUrl = "https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json";

    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of the <see cref="DotNetReleaseIndex"/> class, reading the index over the
    /// network.
    /// </summary>
    public DotNetReleaseIndex()
        : this(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DotNetReleaseIndex"/> class, reading the index through
    /// the given handler.
    /// </summary>
    /// <param name="handler">The handler answering the requests. It is disposed together with this
    /// instance.</param>
    public DotNetReleaseIndex(HttpMessageHandler handler)
    {
        Guard.IsNotNull(handler);
        _http = new HttpClient(handler);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<NetSdkRelease>> GetReleasesAsync(
        NuGetVersion pinnedVersion,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(pinnedVersion);
        var channels = await ReadChannelsAsync(pinnedVersion, cancellationToken).ConfigureAwait(false);
        var releases = new List<NetSdkRelease>();
        foreach (var (releasesUrl, isLts) in channels)
        {
            using var document = await ReadJsonAsync(releasesUrl, cancellationToken).ConfigureAwait(false);
            AddSdkVersions(document.RootElement, isLts, releases);
        }

        return releases;
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose() => _http.Dispose();

    // A channel states its SDK versions twice over: once as the SDK a release shipped with, and once in the
    // list of every SDK of that release. Both are read, and a version stated twice is added once.
    private static void AddSdkVersions(JsonElement channel, bool isLts, List<NetSdkRelease> releases)
    {
        if (!TryGetArray(channel, "releases", out var channelReleases))
        {
            return;
        }

        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var release in channelReleases)
        {
            foreach (var text in EnumerateSdkVersions(release))
            {
                if (known.Add(text) && NuGetVersion.TryParse(text, out var version))
                {
                    releases.Add(new NetSdkRelease(version, isLts));
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateSdkVersions(JsonElement release)
    {
        if (ReadString(release, "sdk", "version") is { Length: > 0 } shipped)
        {
            yield return shipped;
        }

        if (!TryGetArray(release, "sdks", out var sdks))
        {
            yield break;
        }

        foreach (var sdk in sdks)
        {
            if (ReadString(sdk, "version") is { Length: > 0 } version)
            {
                yield return version;
            }
        }
    }

    private static bool TryGetArray(JsonElement parent, string name, out JsonElement.ArrayEnumerator items)
    {
        if (parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out var array)
            && array.ValueKind == JsonValueKind.Array)
        {
            items = array.EnumerateArray();
            return true;
        }

        items = default;
        return false;
    }

    private static string? ReadString(JsonElement parent, string name)
        => parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

    private static string? ReadString(JsonElement parent, string objectName, string name)
        => parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(objectName, out var child)
            ? ReadString(child, name)
            : null;

    // A channel is worth reading when it is the pinned version's own or a newer one. Its version reads as
    // major.minor, which is what a .NET channel is.
    private static bool IsAtOrAbove(string channelVersion, NuGetVersion pinnedVersion)
    {
        if (!NuGetVersion.TryParse(channelVersion, out var channel))
        {
            return false;
        }

        return channel.Major != pinnedVersion.Major ? channel.Major > pinnedVersion.Major : channel.Minor >= pinnedVersion.Minor;
    }

    private async Task<IReadOnlyList<(Uri ReleasesUrl, bool IsLts)>> ReadChannelsAsync(
        NuGetVersion pinnedVersion,
        CancellationToken cancellationToken)
    {
        using var document = await ReadJsonAsync(new Uri(IndexUrl), cancellationToken).ConfigureAwait(false);
        var channels = new List<(Uri ReleasesUrl, bool IsLts)>();
        if (!TryGetArray(document.RootElement, "releases-index", out var entries))
        {
            return channels;
        }

        foreach (var entry in entries)
        {
            var channelVersion = ReadString(entry, "channel-version") ?? string.Empty;
            var releasesUrl = ReadString(entry, "releases.json") ?? string.Empty;
            if (IsAtOrAbove(channelVersion, pinnedVersion) && Uri.TryCreate(releasesUrl, UriKind.Absolute, out var url))
            {
                channels.Add((url, string.Equals(ReadString(entry, "release-type"), "lts", StringComparison.OrdinalIgnoreCase)));
            }
        }

        return channels;
    }

    private async Task<JsonDocument> ReadJsonAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            _ = response.EnsureSuccessStatusCode();
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                return await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (HttpRequestException exception)
        {
            throw new BuildFailedException($"The .NET release index at {url} could not be read: {exception.Message}", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BuildFailedException($"The .NET release index at {url} did not answer in time.", exception);
        }
        catch (JsonException exception)
        {
            throw new BuildFailedException($"The .NET release index at {url} states something bv cannot read: {exception.Message}", exception);
        }
    }
}
