// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Hosts.GitLab;

/// <summary>
/// Performs the operations of the GitLab REST API that <c>bv</c> needs.
/// </summary>
/// <remarks>
/// <para>The API of a host is at <c>https://&lt;host&gt;/api/v4</c>, on <c>gitlab.com</c> and on a self-managed
/// instance alike.</para>
/// <para>Every operation addresses a project by its path, such as <c>group/subgroup/project</c>. The client
/// escapes the path as one segment of the URL, as the API expects.</para>
/// <para>An answer outside the 2xx range fails the build, as <see cref="HostApiClient"/> says. The exceptions
/// are <see cref="GetProjectWithoutCredentialsAsync"/>, where a 404 means that the project is not public, and
/// <see cref="GetProjectMemberAsync"/>, where a 404 means that the user is not a member.</para>
/// </remarks>
internal sealed class GitLabApiClient : HostApiClient
{
    private const string AccessTokenHeader = "PRIVATE-TOKEN";
    private const string JobTokenHeader = "JOB-TOKEN";

    private readonly string _tokenHeader;
    private readonly string _token;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitLabApiClient"/> class.
    /// </summary>
    /// <param name="hostName">The host name of the project, such as <c>gitlab.com</c>.</param>
    /// <param name="token">The token that authenticates every request.</param>
    /// <param name="tokenKind">The kind of <paramref name="token"/>.</param>
    /// <param name="handler">The handler that sends the requests. It is disposed together with this
    /// instance.</param>
    public GitLabApiClient(string hostName, string token, GitLabTokenKind tokenKind, HttpMessageHandler handler)
        : base("GitLab", GetBaseUrl(hostName), handler)
    {
        Guard.IsNotNullOrEmpty(token);

        _tokenHeader = tokenKind switch {
            GitLabTokenKind.AccessToken => AccessTokenHeader,
            GitLabTokenKind.JobToken => JobTokenHeader,
            _ => ThrowHelper.ThrowArgumentOutOfRangeException<string>(nameof(tokenKind), tokenKind, "Unknown token kind."),
        };

        _token = token;
        var headers = DefaultRequestHeaders;
        headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        headers.UserAgent.Add(new ProductInfoHeaderValue("bv", OwnVersion.Value.ToNormalizedString()));
    }

    /// <summary>
    /// Gets a value indicating whether a file name is valid in the generic package registry.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns><see langword="true"/> when <paramref name="fileName"/> holds only ASCII letters, digits, and
    /// <c>. _ - + ~ @</c>, and neither starts nor ends with <c>~</c> or <c>@</c>; otherwise,
    /// <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>The rule is <c>generic_package_file_name_regex</c> in <c>lib/gitlab/regex/packages.rb</c> of
    /// GitLab.</para>
    /// </remarks>
    public static bool IsValidPackageFileName(string fileName)
    {
        Guard.IsNotNull(fileName);
        return fileName is { Length: > 0 }
            && fileName[0] is not ('~' or '@')
            && fileName[^1] is not ('~' or '@')
            && fileName.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or '+' or '~' or '@');
    }

    /// <summary>
    /// Asynchronously gets the user the token belongs to.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> whose result is the user.</returns>
    /// <remarks>
    /// <para>A job token cannot read a user.</para>
    /// </remarks>
    public Task<GitLabUser> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Get, ApiUrl("user"), null, GitLabJsonContext.Default.GitLabUser, cancellationToken);

    /// <summary>
    /// Asynchronously gets a project.
    /// </summary>
    /// <param name="projectPath">The path of the project.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> whose result is the project.</returns>
    /// <remarks>
    /// <para>A job token cannot read a project. Use <see cref="GetProjectWithoutCredentialsAsync"/> with one.</para>
    /// </remarks>
    public Task<GitLabProject> GetProjectAsync(string projectPath, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Get, ApiUrl(ProjectPath(projectPath)), null, GitLabJsonContext.Default.GitLabProject, cancellationToken);

    /// <summary>
    /// Asynchronously gets a project with a request that carries no credentials.
    /// </summary>
    /// <param name="projectPath">The path of the project.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> whose result is the project, or <see langword="null"/> when the API answers
    /// 404.</returns>
    /// <remarks>
    /// <para>GitLab answers 404 for a project that an anonymous request cannot see, so a public project is the
    /// only one this method returns.</para>
    /// </remarks>
    public async Task<GitLabProject?> GetProjectWithoutCredentialsAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        var url = ApiUrl(ProjectPath(projectPath));
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        return await SendOrNullAsync(request, url, GitLabJsonContext.Default.GitLabProject, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously gets the membership of a user in a project.
    /// </summary>
    /// <param name="projectPath">The path of the project.</param>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> whose result is the membership, or <see langword="null"/> when the API answers
    /// 404.</returns>
    /// <remarks>
    /// <para>The endpoint is <c>members/all</c>, which counts a direct membership, a membership of a parent group,
    /// and a membership of a group the project or a parent group is shared with. The access level it states is
    /// the one GitLab enforces on the project.</para>
    /// <para>GitLab answers 404 for a user with no membership, such as an instance administrator, and for a
    /// project the token cannot see.</para>
    /// <para>A job token cannot read a membership.</para>
    /// </remarks>
    public async Task<GitLabMember?> GetProjectMemberAsync(
        string projectPath,
        long userId,
        CancellationToken cancellationToken = default)
    {
        var url = ApiUrl(ProjectPath(projectPath) + "/members/all/" + userId.ToString(CultureInfo.InvariantCulture));
        using var request = CreateRequest(HttpMethod.Get, url);
        return await SendOrNullAsync(request, url, GitLabJsonContext.Default.GitLabMember, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously gets the latest releases of a project.
    /// </summary>
    /// <param name="projectPath">The path of the project.</param>
    /// <param name="count">The number of releases to get, from 1 to 100.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> whose result is the releases, latest first. The list holds fewer than
    /// <paramref name="count"/> releases when the project has fewer.</returns>
    public Task<IReadOnlyList<GitLabRelease>> GetReleasesAsync(
        string projectPath,
        int count,
        CancellationToken cancellationToken = default)
    {
        Guard.IsInRange(count, 1, 101);
        var query = string.Create(CultureInfo.InvariantCulture, $"?per_page={count}");
        return SendAsync(
            HttpMethod.Get,
            ApiUrl(ProjectPath(projectPath) + "/releases" + query),
            null,
            GitLabJsonContext.Default.IReadOnlyListGitLabRelease,
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously creates a release.
    /// </summary>
    /// <param name="projectPath">The path of the project.</param>
    /// <param name="release">The members of the new release.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> whose result is the created release.</returns>
    public Task<GitLabRelease> CreateReleaseAsync(
        string projectPath,
        GitLabNewRelease release,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(release);
        return SendAsync(
            HttpMethod.Post,
            ApiUrl(ProjectPath(projectPath) + "/releases"),
            JsonBody(release, GitLabJsonContext.Default.GitLabNewRelease),
            GitLabJsonContext.Default.GitLabRelease,
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously deletes a release.
    /// </summary>
    /// <param name="projectPath">The path of the project.</param>
    /// <param name="tagName">The name of the tag of the release.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    /// <remarks>
    /// <para>The tag of the release stays.</para>
    /// </remarks>
    public Task DeleteReleaseAsync(string projectPath, string tagName, CancellationToken cancellationToken = default)
    {
        Guard.IsNotNullOrEmpty(tagName);
        return SendAsync(
            HttpMethod.Delete,
            ApiUrl(ProjectPath(projectPath) + "/releases/" + Uri.EscapeDataString(tagName)),
            null,
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously uploads a file of a generic package, and creates the package when it does not exist.
    /// </summary>
    /// <param name="projectPath">The path of the project.</param>
    /// <param name="packageName">The name of the package.</param>
    /// <param name="packageVersion">The version of the package.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="contents">The contents of the file. The stream is disposed together with the request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> whose result is the uploaded file.</returns>
    /// <exception cref="BuildFailedException"><paramref name="fileName"/> is not valid in the generic package
    /// registry, as <see cref="IsValidPackageFileName"/> says.</exception>
    public Task<GitLabPackageFile> UploadPackageFileAsync(
        string projectPath,
        string packageName,
        string packageVersion,
        string fileName,
        Stream contents,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(contents);
        BuildFailedException.ThrowIfNot(
            IsValidPackageFileName(fileName),
            $"'{fileName}' is not a valid file name in the GitLab generic package registry.");

        // The answer of a plain upload has no body. With select=package_file, it holds the uploaded file.
        var url = new Uri(GetPackageFileUrl(projectPath, packageName, packageVersion, fileName).AbsoluteUri + "?select=package_file");
        var content = new StreamContent(contents);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return SendAsync(HttpMethod.Put, url, content, GitLabJsonContext.Default.GitLabPackageFile, cancellationToken);
    }

    /// <summary>
    /// Gets the URL of a file of a generic package, which is also the URL a file is uploaded to.
    /// </summary>
    /// <param name="projectPath">The path of the project.</param>
    /// <param name="packageName">The name of the package.</param>
    /// <param name="packageVersion">The version of the package.</param>
    /// <param name="fileName">The file name.</param>
    /// <returns>The URL of the file.</returns>
    public Uri GetPackageFileUrl(string projectPath, string packageName, string packageVersion, string fileName)
    {
        Guard.IsNotNullOrEmpty(packageName);
        Guard.IsNotNullOrEmpty(packageVersion);
        Guard.IsNotNullOrEmpty(fileName);
        var path = $"{ProjectPath(projectPath)}/packages/generic/{Uri.EscapeDataString(packageName)}/"
            + $"{Uri.EscapeDataString(packageVersion)}/{Uri.EscapeDataString(fileName)}";
        return ApiUrl(path);
    }

    /// <summary>
    /// Asynchronously deletes a package, with all its files.
    /// </summary>
    /// <param name="projectPath">The path of the project.</param>
    /// <param name="packageId">The identifier of the package.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public Task DeletePackageAsync(string projectPath, long packageId, CancellationToken cancellationToken = default)
        => SendAsync(
            HttpMethod.Delete,
            ApiUrl(ProjectPath(projectPath) + "/packages/" + packageId.ToString(CultureInfo.InvariantCulture)),
            null,
            cancellationToken);

    /// <inheritdoc/>
    protected override void Authenticate(HttpRequestMessage request)
    {
        Guard.IsNotNull(request);
        request.Headers.Add(_tokenHeader, _token);
    }

    /// <inheritdoc/>
    protected override async Task<string?> ReadErrorMessageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        // A GitLab error answer is JSON. Its "message" member is a string, an array of strings, or an object
        // that maps each member of the request to its errors. Some answers state an "error" member instead.
        // A proxy in front of a server may answer with anything else, and then the status code is the whole
        // report.
        Guard.IsNotNull(response);
        JsonDocument document;
        try
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                document = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return Quote(root, "message") ?? Quote(root, "error");
        }
    }

    private static Uri GetBaseUrl(string hostName)
    {
        Guard.IsNotNullOrEmpty(hostName);
        return new Uri($"https://{hostName}/api/v4/");
    }

    private static string ProjectPath(string projectPath)
    {
        Guard.IsNotNullOrEmpty(projectPath);
        return "projects/" + Uri.EscapeDataString(projectPath);
    }

    // A string is quoted as it is. An array or an object is quoted as JSON, because its shape varies with the
    // endpoint, and the raw text is what a reader can match against the GitLab documentation.
    private static string? Quote(JsonElement root, string memberName)
    {
        if (!root.TryGetProperty(memberName, out var value))
        {
            return null;
        }

        var text = value.ValueKind switch {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Null => null,
            _ => value.GetRawText(),
        };

        return text is { Length: > 0 } ? text : null;
    }

    private async Task<T?> SendOrNullAsync<T>(
        HttpRequestMessage request,
        Uri url,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
        where T : class
    {
        using var response = await SendCoreAsync(request, url, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(request.Method, url, response, cancellationToken).ConfigureAwait(false);
        return await ReadAsync(request.Method, url, response, typeInfo, cancellationToken).ConfigureAwait(false);
    }
}
