// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Hosts;

/// <summary>
/// Base class for the clients of the REST API of a Git host.
/// </summary>
/// <remarks>
/// <para>An answer outside the 2xx range fails the build, with the status code and the message of the answer.
/// A derived class that expects another status code sends the request with <see cref="SendCoreAsync"/>, and
/// checks the code before it calls <see cref="EnsureSuccessAsync"/>.</para>
/// <para>Every diagnostic names the API after <see cref="ApiName"/>, with the method and the URL of the
/// request.</para>
/// <para>A request carries the credentials of the client when <see cref="CreateRequest"/> creates it. A request
/// that a derived class creates otherwise carries none.</para>
/// </remarks>
internal abstract class HostApiClient : IDisposable
{
    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostApiClient"/> class.
    /// </summary>
    /// <param name="apiName">The name of the API in diagnostics, such as <c>GitHub</c>.</param>
    /// <param name="baseUrl">The base URL of the API, with a trailing slash.</param>
    /// <param name="handler">The handler that sends the requests. It is disposed together with this
    /// instance.</param>
    protected HostApiClient(string apiName, Uri baseUrl, HttpMessageHandler handler)
    {
        Guard.IsNotNullOrEmpty(apiName);
        Guard.IsNotNull(baseUrl);
        Guard.IsNotNull(handler);

        ApiName = apiName;
        BaseUrl = baseUrl;
        _http = new HttpClient(handler);
    }

    /// <summary>
    /// Gets the base URL of the API, with a trailing slash.
    /// </summary>
    public Uri BaseUrl { get; }

    /// <summary>
    /// Gets the name of the API in diagnostics.
    /// </summary>
    protected string ApiName { get; }

    /// <summary>
    /// Gets the headers that every request carries.
    /// </summary>
    protected HttpRequestHeaders DefaultRequestHeaders => _http.DefaultRequestHeaders;

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Serializes a value as the JSON body of a request.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="typeInfo">The serialization metadata of <typeparamref name="T"/>.</param>
    /// <returns>A newly-created <see cref="StringContent"/>.</returns>
    protected static StringContent JsonBody<T>(T value, JsonTypeInfo<T> typeInfo)
        => new(JsonSerializer.Serialize(value, typeInfo), Encoding.UTF8, "application/json");

    /// <summary>
    /// Builds the absolute URL of an endpoint.
    /// </summary>
    /// <param name="path">The path of the endpoint, relative to <see cref="BaseUrl"/>.</param>
    /// <returns>The URL of the endpoint.</returns>
    /// <remarks>
    /// <para>Every request goes to an absolute URL that the client holds and passes along with the request,
    /// so that a diagnostic names the URL without reading back the nullable
    /// <see cref="HttpRequestMessage.RequestUri"/>.</para>
    /// </remarks>
    protected Uri ApiUrl(string path) => new(BaseUrl, path);

    /// <summary>
    /// Asynchronously sends a request, and reads the JSON answer.
    /// </summary>
    /// <typeparam name="T">The type of the answer.</typeparam>
    /// <param name="method">The method of the request.</param>
    /// <param name="url">The URL of the request.</param>
    /// <param name="content">The body of the request, or <see langword="null"/> for a request without one.</param>
    /// <param name="typeInfo">The serialization metadata of <typeparamref name="T"/>.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> whose result is the answer.</returns>
    protected async Task<T> SendAsync<T>(
        HttpMethod method,
        Uri url,
        HttpContent? content,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
        where T : class
    {
        using var request = CreateRequest(method, url);
        request.Content = content;
        using var response = await SendCoreAsync(request, url, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(method, url, response, cancellationToken).ConfigureAwait(false);
        return await ReadAsync(method, url, response, typeInfo, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously sends a request whose answer has no body to read.
    /// </summary>
    /// <param name="method">The method of the request.</param>
    /// <param name="url">The URL of the request.</param>
    /// <param name="content">The body of the request, or <see langword="null"/> for a request without one.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    protected async Task SendAsync(
        HttpMethod method,
        Uri url,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, url);
        request.Content = content;
        using var response = await SendCoreAsync(request, url, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(method, url, response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a request that carries the credentials of the client.
    /// </summary>
    /// <param name="method">The method of the request.</param>
    /// <param name="url">The URL of the request.</param>
    /// <returns>A newly-created <see cref="HttpRequestMessage"/>.</returns>
    protected HttpRequestMessage CreateRequest(HttpMethod method, Uri url)
    {
        var request = new HttpRequestMessage(method, url);
        Authenticate(request);
        return request;
    }

    /// <summary>
    /// Adds the credentials of the client to a request.
    /// </summary>
    /// <param name="request">The request.</param>
    protected abstract void Authenticate(HttpRequestMessage request);

    /// <summary>
    /// Asynchronously sends a request, and returns the answer whatever its status code.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="url">The URL of the request.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> whose result is the answer.</returns>
    /// <exception cref="BuildFailedException">The API cannot be reached, or does not answer in time.</exception>
    protected async Task<HttpResponseMessage> SendCoreAsync(
        HttpRequestMessage request,
        Uri url,
        CancellationToken cancellationToken)
    {
        Guard.IsNotNull(request);
        try
        {
            return await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new BuildFailedException(
                $"The {ApiName} API could not be reached for {Describe(request.Method, url)}: {exception.Message}",
                exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BuildFailedException($"The {ApiName} API did not answer {Describe(request.Method, url)} in time.", exception);
        }
    }

    /// <summary>
    /// Asynchronously fails the build when an answer is outside the 2xx range.
    /// </summary>
    /// <param name="method">The method of the request.</param>
    /// <param name="url">The URL of the request.</param>
    /// <param name="response">The answer.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    /// <exception cref="BuildFailedException">The status code of <paramref name="response"/> is outside the
    /// 2xx range.</exception>
    protected async Task EnsureSuccessAsync(
        HttpMethod method,
        Uri url,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        Guard.IsNotNull(response);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = await ReadErrorMessageAsync(response, cancellationToken).ConfigureAwait(false);
        var status = string.Create(CultureInfo.InvariantCulture, $"{(int)response.StatusCode} {response.ReasonPhrase}");
        throw new BuildFailedException(message is null
            ? $"The {ApiName} API answered {Describe(method, url)} with {status}."
            : $"The {ApiName} API answered {Describe(method, url)} with {status}: {message}");
    }

    /// <summary>
    /// Asynchronously reads the message of an error answer.
    /// </summary>
    /// <param name="response">The answer, whose status code is outside the 2xx range.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> whose result is the message, or <see langword="null"/> when the answer
    /// states none that the client can read.</returns>
    protected abstract Task<string?> ReadErrorMessageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken);

    /// <summary>
    /// Asynchronously reads the JSON answer of a request.
    /// </summary>
    /// <typeparam name="T">The type of the answer.</typeparam>
    /// <param name="method">The method of the request.</param>
    /// <param name="url">The URL of the request.</param>
    /// <param name="response">The answer.</param>
    /// <param name="typeInfo">The serialization metadata of <typeparamref name="T"/>.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A <see cref="Task"/> whose result is the answer.</returns>
    /// <exception cref="BuildFailedException">The answer is not JSON of type <typeparamref name="T"/>, or is
    /// <c>null</c>.</exception>
    /// <remarks>
    /// <para>The client buffers every answer before <see cref="SendCoreAsync"/> returns, so reading the body
    /// meets no network.</para>
    /// </remarks>
    protected async Task<T> ReadAsync<T>(
        HttpMethod method,
        Uri url,
        HttpResponseMessage response,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
        where T : class
    {
        Guard.IsNotNull(response);
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
                $"The {ApiName} API answered {Describe(method, url)} with something bv cannot read: {exception.Message}",
                exception);
        }

        return result ?? throw new BuildFailedException($"The {ApiName} API answered {Describe(method, url)} with null.");
    }

    /// <summary>
    /// Releases the resources of this instance.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _http.Dispose();
        }
    }

    private static string Describe(HttpMethod method, Uri url) => $"{method} {url.AbsoluteUri}";
}
