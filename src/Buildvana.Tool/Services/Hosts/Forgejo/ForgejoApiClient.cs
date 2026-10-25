// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Hosts.Forgejo;

/// <summary>
/// Performs the operations of the Forgejo REST API that <c>bv</c> needs.
/// </summary>
/// <remarks>
/// <para>The API of a host is at <c>https://&lt;host&gt;/api/v1</c>.</para>
/// <para>An answer outside the 2xx range fails the build, as <see cref="HostApiClient"/> says. The exceptions
/// are <see cref="DeleteReleaseAsync"/>, where a 404 means that the release does not exist, and
/// <see cref="DeleteTagAsync"/>, where a 404 means that the tag does not exist.</para>
/// </remarks>
internal sealed class ForgejoApiClient : HostApiClient
{
    private const string AttachmentFieldName = "attachment";

    private readonly AuthenticationHeaderValue _authorization;

    /// <summary>
    /// Initializes a new instance of the <see cref="ForgejoApiClient"/> class.
    /// </summary>
    /// <param name="hostName">The host name of the repository, such as <c>codeberg.org</c>.</param>
    /// <param name="token">The token that authenticates every request.</param>
    /// <param name="handler">The handler that sends the requests. It is disposed together with this
    /// instance.</param>
    public ForgejoApiClient(string hostName, string token, HttpMessageHandler handler)
        : base("Forgejo", GetBaseUrl(hostName), handler)
    {
        Guard.IsNotNullOrEmpty(token);

        _authorization = new AuthenticationHeaderValue("token", token);
        var headers = DefaultRequestHeaders;
        headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        headers.UserAgent.Add(new ProductInfoHeaderValue("bv", OwnVersion.Value.ToNormalizedString()));
    }

    /// <summary>
    /// Asynchronously gets a repository.
    /// </summary>
    /// <param name="owner">The owner of the repository.</param>
    /// <param name="name">The name of the repository.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> whose result is the repository.</returns>
    public Task<ForgejoRepository> GetRepositoryAsync(
        string owner,
        string name,
        CancellationToken cancellationToken = default)
        => SendAsync(
            HttpMethod.Get,
            ApiUrl(RepositoryPath(owner, name)),
            null,
            ForgejoJsonContext.Default.ForgejoRepository,
            cancellationToken);

    /// <summary>
    /// Asynchronously creates a release.
    /// </summary>
    /// <param name="owner">The owner of the repository.</param>
    /// <param name="name">The name of the repository.</param>
    /// <param name="release">The members of the new release.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> whose result is the created release.</returns>
    /// <remarks>
    /// <para>On a tag that has no release, Forgejo turns the tag into the new release. A draft creates no tag.</para>
    /// </remarks>
    public Task<ForgejoRelease> CreateReleaseAsync(
        string owner,
        string name,
        ForgejoNewRelease release,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(release);
        return SendAsync(
            HttpMethod.Post,
            ApiUrl(RepositoryPath(owner, name) + "/releases"),
            JsonBody(release, ForgejoJsonContext.Default.ForgejoNewRelease),
            ForgejoJsonContext.Default.ForgejoRelease,
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
        ForgejoReleaseUpdate update,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(update);
        return SendAsync(
            HttpMethod.Patch,
            ApiUrl(ReleasePath(owner, name, releaseId)),
            JsonBody(update, ForgejoJsonContext.Default.ForgejoReleaseUpdate),
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously deletes a release.
    /// </summary>
    /// <param name="owner">The owner of the repository.</param>
    /// <param name="name">The name of the repository.</param>
    /// <param name="releaseId">The identifier of the release.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> whose result is <see langword="true"/> when the release was deleted, and
    /// <see langword="false"/> when the API answers 404.</returns>
    /// <remarks>
    /// <para>Forgejo deletes the assets of the release, and keeps its tag. <see cref="DeleteTagAsync"/> can then
    /// delete the tag.</para>
    /// </remarks>
    public Task<bool> DeleteReleaseAsync(
        string owner,
        string name,
        long releaseId,
        CancellationToken cancellationToken = default)
        => DeleteAsync(ApiUrl(ReleasePath(owner, name, releaseId)), cancellationToken);

    /// <summary>
    /// Asynchronously deletes a tag that has no release.
    /// </summary>
    /// <param name="owner">The owner of the repository.</param>
    /// <param name="name">The name of the repository.</param>
    /// <param name="tagName">The name of the tag.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> whose result is <see langword="true"/> when the tag was deleted, and
    /// <see langword="false"/> when the API answers 404.</returns>
    /// <remarks>
    /// <para>Forgejo refuses to delete a tag that has a release, with 409.</para>
    /// </remarks>
    public Task<bool> DeleteTagAsync(
        string owner,
        string name,
        string tagName,
        CancellationToken cancellationToken = default)
        => DeleteAsync(ApiUrl(RepositoryPath(owner, name) + "/tags/" + EscapeRefName(tagName)), cancellationToken);

    /// <summary>
    /// Asynchronously uploads an asset of a release.
    /// </summary>
    /// <param name="owner">The owner of the repository.</param>
    /// <param name="name">The name of the repository.</param>
    /// <param name="releaseId">The identifier of the release.</param>
    /// <param name="fileName">The file name of the asset.</param>
    /// <param name="contents">The contents of the asset. The stream is disposed together with the request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    /// <remarks>
    /// <para>Forgejo accepts two assets of a release with the same file name. It answers 413 when the upload
    /// exceeds the quota of the repository.</para>
    /// </remarks>
    public Task UploadReleaseAssetAsync(
        string owner,
        string name,
        long releaseId,
        string fileName,
        Stream contents,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNullOrEmpty(fileName);
        Guard.IsNotNull(contents);

        // The name query parameter takes precedence over the file name of the form field.
        var url = ApiUrl(ReleasePath(owner, name, releaseId) + "/assets?name=" + Uri.EscapeDataString(fileName));
        var content = new MultipartFormDataContent();
#pragma warning disable CA2000 // Dispose objects before losing scope — the multipart content owns and disposes its parts
        var file = new StreamContent(contents);
#pragma warning restore CA2000
        content.Add(file, AttachmentFieldName, fileName);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return SendAsync(HttpMethod.Post, url, content, cancellationToken);
    }

    /// <inheritdoc/>
    protected override void Authenticate(HttpRequestMessage request)
    {
        Guard.IsNotNull(request);
        request.Headers.Authorization = _authorization;
    }

    /// <inheritdoc/>
    protected override async Task<string?> ReadErrorMessageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        // A Forgejo error answer is JSON with a "message" member. A proxy in front of a server may answer with
        // anything else, and then the status code is the whole report.
        Guard.IsNotNull(response);
        ForgejoErrorBody? body;
        try
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                body = await JsonSerializer.DeserializeAsync(stream, ForgejoJsonContext.Default.ForgejoErrorBody, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return body?.Message is { Length: > 0 } message ? message : null;
    }

    private static Uri GetBaseUrl(string hostName)
    {
        Guard.IsNotNullOrEmpty(hostName);
        return new Uri($"https://{hostName}/api/v1/");
    }

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

    private async Task<bool> DeleteAsync(Uri url, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Delete, url);
        using var response = await SendCoreAsync(request, url, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureSuccessAsync(HttpMethod.Delete, url, response, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
