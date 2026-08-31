// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http;

/// <summary>
/// Answers requests from a table of canned bodies, and records what was asked.
/// </summary>
/// <remarks>
/// <para>An address the table does not name is answered as not found, which is what a test of a reader's
/// failure path needs.</para>
/// </remarks>
internal sealed class StubHttpMessageHandler(IReadOnlyDictionary<string, string> answers) : HttpMessageHandler
{
    /// <summary>Gets the addresses that were requested, in request order.</summary>
    public List<string> Requests { get; } = [];

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.AbsoluteUri;
        Requests.Add(url);
        var response = answers.TryGetValue(url, out var body)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }
            : new HttpResponseMessage(HttpStatusCode.NotFound);
        return Task.FromResult(response);
    }
}
