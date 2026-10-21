// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

/// <summary>
/// A request that <see cref="StubHttpMessageHandler"/> received.
/// </summary>
/// <param name="Method">The method of the request.</param>
/// <param name="Url">The absolute URL of the request.</param>
/// <param name="Headers">The headers of the request and of its content, by case-insensitive name. The values
/// of a header are joined with <c>", "</c>.</param>
/// <param name="Body">The body of the request, or <see langword="null"/> for a request without content.</param>
internal sealed record StubHttpRequest(
    HttpMethod Method,
    string Url,
    IReadOnlyDictionary<string, string> Headers,
    string? Body);
