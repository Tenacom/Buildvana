// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Headers;

/// <summary>
/// Answers requests from a table of canned answers, and records what was asked.
/// </summary>
/// <remarks>
/// <para>A request the table does not name gets the fallback answer, which is 404 by default. A not-found
/// answer is what a test of a reader's failure path needs.</para>
/// </remarks>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly IReadOnlyDictionary<(HttpMethod Method, string Url), StubHttpAnswer> _answers;
    private readonly StubHttpAnswer _fallback;

    /// <summary>
    /// Initializes a new instance of the <see cref="StubHttpMessageHandler"/> class, answering requests by
    /// method and URL.
    /// </summary>
    /// <param name="answers">The answers, by method and absolute URL.</param>
    /// <param name="fallback">The answer to a request that <paramref name="answers"/> does not name, or
    /// <see langword="null"/> for 404.</param>
    public StubHttpMessageHandler(
        IReadOnlyDictionary<(HttpMethod Method, string Url), StubHttpAnswer> answers,
        StubHttpAnswer? fallback = null)
    {
        _answers = answers;
        _fallback = fallback ?? new StubHttpAnswer(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StubHttpMessageHandler"/> class, answering GET requests
    /// with 200 and a body.
    /// </summary>
    /// <param name="bodies">The bodies of the answers, by absolute URL.</param>
    public StubHttpMessageHandler(IReadOnlyDictionary<string, string> bodies)
        : this(bodies.ToDictionary(
            static pair => (HttpMethod.Get, pair.Key),
            static pair => new StubHttpAnswer(HttpStatusCode.OK, pair.Value)))
    {
    }

    /// <summary>Gets the requests that were received, in request order.</summary>
    public List<StubHttpRequest> Requests { get; } = [];

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var url = request.RequestUri!.AbsoluteUri;
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        Requests.Add(new StubHttpRequest(request.Method, url, CollectHeaders(request), body));

        var answer = _answers.GetValueOrDefault((request.Method, url), _fallback);
        if (answer.Failure is not null)
        {
            throw answer.Failure;
        }

        var response = new HttpResponseMessage(answer.Status);
        if (answer.Body is not null)
        {
            response.Content = new StringContent(answer.Body);
        }

        return response;
    }

    private static Dictionary<string, string> CollectHeaders(HttpRequestMessage request)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Add(request.Headers);
        if (request.Content is not null)
        {
            Add(request.Content.Headers);
        }

        return headers;

        void Add(HttpHeaders source)
        {
            foreach (var (name, values) in source)
            {
                headers[name] = string.Join(", ", values);
            }
        }
    }
}
