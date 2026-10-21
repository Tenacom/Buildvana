// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.ServerAdapters.Internal.GitHub;

/// <summary>
/// Performs the operations of the GitHub REST API that <c>bv</c> needs.
/// </summary>
/// <remarks>
/// <para>The API of <c>github.com</c> is at <c>https://api.github.com</c>. Any other host is taken for a
/// GitHub Enterprise Server, whose API is at <c>https://&lt;host&gt;/api/v3</c>.</para>
/// <para>An asset upload goes to the address that the release states in its <c>upload_url</c> member, so this
/// type derives no upload host.</para>
/// <para>An answer outside the 2xx range fails the build, with the status code and the message of the answer.
/// The one exception is a tag lookup, where a 404 means that the tag does not exist.</para>
/// </remarks>
internal sealed class GitHubApiClient : IDisposable
{
    /// <summary>
    /// The version of the REST API that every request asks for.
    /// </summary>
    /// <remarks>
    /// <para>GitHub Enterprise Server 3.19 and 3.20 support this version alone, and answer a request for a newer
    /// one with 410 Gone. Version 2026-03-10 changes none of the members or endpoints that this type uses.
    /// Move to it once GitHub Enterprise Server 3.20 closes down on 2027-03-17, and before this version
    /// closes down on 2028-03-10.</para>
    /// </remarks>
    public const string ApiVersion = "2022-11-28";

    private const string GitHubComHostName = "github.com";
    private const string GitHubMediaType = "application/vnd.github+json";

    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubApiClient"/> class.
    /// </summary>
    /// <param name="hostName">The host name of the repository, such as <c>github.com</c>.</param>
    /// <param name="token">The token that authenticates every request.</param>
    /// <param name="handler">The handler that sends the requests. It is disposed together with this
    /// instance.</param>
    public GitHubApiClient(string hostName, string token, HttpMessageHandler handler)
    {
        Guard.IsNotNullOrEmpty(hostName);
        Guard.IsNotNullOrEmpty(token);
        Guard.IsNotNull(handler);

        BaseUrl = GetBaseUrl(hostName);
        _http = new HttpClient(handler) { BaseAddress = BaseUrl };
        var headers = _http.DefaultRequestHeaders;
        headers.Accept.Add(new MediaTypeWithQualityHeaderValue(GitHubMediaType));
        headers.Add("X-GitHub-Api-Version", ApiVersion);
        headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        headers.UserAgent.Add(new ProductInfoHeaderValue("bv", OwnVersion.Value.ToNormalizedString()));
    }

    /// <summary>
    /// Gets the base URL of the API, with a trailing slash.
    /// </summary>
    public Uri BaseUrl { get; }

    /// <summary>
    /// Asynchronously gets a repository.
    /// </summary>
    /// <param name="owner">The owner of the repository.</param>
    /// <param name="name">The name of the repository.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> whose result is the repository.</returns>
    public Task<GitHubRepository> GetRepositoryAsync(
        string owner,
        string name,
        CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Get, RepositoryPath(owner, name), null, GitHubJsonContext.Default.GitHubRepository, cancellationToken);

    /// <summary>
    /// Asynchronously creates a release.
    /// </summary>
    /// <param name="owner">The owner of the repository.</param>
    /// <param name="name">The name of the repository.</param>
    /// <param name="release">The members of the new release.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> whose result is the created release.</returns>
    public Task<GitHubRelease> CreateReleaseAsync(
        string owner,
        string name,
        GitHubNewRelease release,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(release);
        return SendAsync(
            HttpMethod.Post,
            RepositoryPath(owner, name) + "/releases",
            JsonBody(release, GitHubJsonContext.Default.GitHubNewRelease),
            GitHubJsonContext.Default.GitHubRelease,
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously generates the notes of a release.
    /// </summary>
    /// <param name="owner">The owner of the repository.</param>
    /// <param name="name">The name of the repository.</param>
    /// <param name="request">The members of the request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> whose result is the generated notes.</returns>
    public Task<GitHubReleaseNotes> GenerateReleaseNotesAsync(
        string owner,
        string name,
        GitHubReleaseNotesRequest request,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(request);
        return SendAsync(
            HttpMethod.Post,
            RepositoryPath(owner, name) + "/releases/generate-notes",
            JsonBody(request, GitHubJsonContext.Default.GitHubReleaseNotesRequest),
            GitHubJsonContext.Default.GitHubReleaseNotes,
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously edits a release.
    /// </summary>
    /// <param name="owner">The owner of the repository.</param>
    /// <param name="name">The name of the repository.</param>
    /// <param name="releaseId">The identifier of the release.</param>
    /// <param name="update">The new members of the release.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public Task UpdateReleaseAsync(
        string owner,
        string name,
        long releaseId,
        GitHubReleaseUpdate update,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(update);
        return SendAsync(
            HttpMethod.Patch,
            ReleasePath(owner, name, releaseId),
            JsonBody(update, GitHubJsonContext.Default.GitHubReleaseUpdate),
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously deletes a release.
    /// </summary>
    /// <param name="owner">The owner of the repository.</param>
    /// <param name="name">The name of the repository.</param>
    /// <param name="releaseId">The identifier of the release.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public Task DeleteReleaseAsync(
        string owner,
        string name,
        long releaseId,
        CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Delete, ReleasePath(owner, name, releaseId), null, cancellationToken);

    /// <summary>
    /// Asynchronously gets a value indicating whether a tag exists.
    /// </summary>
    /// <param name="owner">The owner of the repository.</param>
    /// <param name="name">The name of the repository.</param>
    /// <param name="tagName">The name of the tag.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> whose result is <see langword="true"/> when the tag exists, and
    /// <see langword="false"/> when it does not.</returns>
    /// <remarks>
    /// <para>The lookup asks for <c>git/ref/tags/&lt;tag&gt;</c>, which matches the tag name exactly. The
    /// <c>git/refs/tags/&lt;tag&gt;</c> form would answer with every tag whose name starts with
    /// <paramref name="tagName"/> when no tag matches exactly.</para>
    /// </remarks>
    public async Task<bool> TagExistsAsync(
        string owner,
        string name,
        string tagName,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, RepositoryPath(owner, name) + "/git/ref/tags/" + EscapeRefName(tagName));
        using var response = await SendCoreAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureSuccessAsync(request, response, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Asynchronously deletes a tag.
    /// </summary>
    /// <param name="owner">The owner of the repository.</param>
    /// <param name="name">The name of the repository.</param>
    /// <param name="tagName">The name of the tag.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public Task DeleteTagAsync(string owner, string name, string tagName, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Delete, RepositoryPath(owner, name) + "/git/refs/tags/" + EscapeRefName(tagName), null, cancellationToken);

    /// <summary>
    /// Asynchronously uploads an asset of a release.
    /// </summary>
    /// <param name="release">The release.</param>
    /// <param name="fileName">The file name of the asset.</param>
    /// <param name="contentType">The MIME type of the asset.</param>
    /// <param name="label">The label of the asset, or <see langword="null"/> for an asset without a label.</param>
    /// <param name="contents">The contents of the asset. The stream is disposed together with the request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public Task UploadReleaseAssetAsync(
        GitHubRelease release,
        string fileName,
        string contentType,
        string? label,
        Stream contents,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(release);
        Guard.IsNotNullOrEmpty(fileName);
        Guard.IsNotNullOrEmpty(contentType);
        Guard.IsNotNull(contents);

        var url = GetUploadUrl(release, fileName, label);
        BuildFailedException.ThrowIfNot(
            MediaTypeHeaderValue.TryParse(contentType, out var mediaType),
            $"'{contentType}' is not a valid MIME type for the release asset {fileName}.");

        var content = new StreamContent(contents);
        content.Headers.ContentType = mediaType;
        return SendAsync(HttpMethod.Post, url.AbsoluteUri, content, cancellationToken);
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose() => _http.Dispose();

    private static Uri GetBaseUrl(string hostName)
        => string.Equals(hostName, GitHubComHostName, StringComparison.OrdinalIgnoreCase)
            ? new Uri("https://api.github.com/")
            : new Uri($"https://{hostName}/api/v3/");

    private static string ReleasePath(string owner, string name, long releaseId)
        => RepositoryPath(owner, name) + "/releases/" + releaseId.ToString(CultureInfo.InvariantCulture);

    private static string RepositoryPath(string owner, string name)
    {
        Guard.IsNotNullOrEmpty(owner);
        Guard.IsNotNullOrEmpty(name);
        return $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}";
    }

    // A tag name may hold slashes, and the API expects them as path separators. Every other character is
    // escaped within its segment.
    private static string EscapeRefName(string tagName)
    {
        Guard.IsNotNullOrEmpty(tagName);
        return string.Join('/', tagName.Split('/').Select(Uri.EscapeDataString));
    }

    // The upload_url member is a URI template, such as ".../releases/1/assets{?name,label}". The template
    // expression is dropped, and the query is written here.
    private static Uri GetUploadUrl(GitHubRelease release, string fileName, string? label)
    {
        var template = release.UploadUrl;
        var expressionStart = template.IndexOf('{', StringComparison.Ordinal);
        var address = expressionStart < 0 ? template : template[..expressionStart];
        var query = "?name=" + Uri.EscapeDataString(fileName);
        if (!string.IsNullOrEmpty(label))
        {
            query += "&label=" + Uri.EscapeDataString(label);
        }

        BuildFailedException.ThrowIfNot(
            Uri.TryCreate(address + query, UriKind.Absolute, out var url),
            $"The upload URL of release {release.Id}, '{template}', is not an absolute URL.");

        return url;
    }

    private static StringContent JsonBody<T>(T value, JsonTypeInfo<T> typeInfo)
        => new(JsonSerializer.Serialize(value, typeInfo), Encoding.UTF8, "application/json");

    private static async Task EnsureSuccessAsync(
        HttpRequestMessage request,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = await ReadErrorMessageAsync(response, cancellationToken).ConfigureAwait(false);
        var status = string.Create(CultureInfo.InvariantCulture, $"{(int)response.StatusCode} {response.ReasonPhrase}");
        throw new BuildFailedException(message is null
            ? $"The GitHub API answered {Describe(request)} with {status}."
            : $"The GitHub API answered {Describe(request)} with {status}: {message}");
    }

    // A GitHub error answer is JSON with a "message" member. A proxy in front of a server may answer with
    // anything else, and then the status code is the whole report.
    private static async Task<string?> ReadErrorMessageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                var body = await JsonSerializer.DeserializeAsync(stream, GitHubJsonContext.Default.GitHubErrorBody, cancellationToken)
                    .ConfigureAwait(false);
                return body?.Message is { Length: > 0 } message ? message : null;
            }
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // The client buffers every answer before SendAsync returns, so reading the body here meets no network.
    private static async Task<T> ReadAsync<T>(
        HttpRequestMessage request,
        HttpResponseMessage response,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
        where T : class
    {
        T? result;
        try
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                result = await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (JsonException exception)
        {
            throw new BuildFailedException(
                $"The GitHub API answered {Describe(request)} with something bv cannot read: {exception.Message}",
                exception);
        }

        return result ?? throw new BuildFailedException($"The GitHub API answered {Describe(request)} with null.");
    }

    private static string Describe(HttpRequestMessage request) => $"{request.Method} {request.RequestUri?.AbsoluteUri}";

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string url,
        HttpContent? content,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
        where T : class
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        using var response = await SendCoreAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(request, response, cancellationToken).ConfigureAwait(false);
        return await ReadAsync(request, response, typeInfo, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendAsync(
        HttpMethod method,
        string url,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        using var response = await SendCoreAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(request, response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendCoreAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new BuildFailedException($"The GitHub API could not be reached for {Describe(request)}: {exception.Message}", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BuildFailedException($"The GitHub API did not answer {Describe(request)} in time.", exception);
        }
    }
}
