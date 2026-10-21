// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;

/// <summary>
/// An answer of <see cref="StubHttpMessageHandler"/>: a status code and a body, or an exception that the
/// request throws.
/// </summary>
/// <param name="Status">The status code of the answer.</param>
/// <param name="Body">The body of the answer, or <see langword="null"/> for an empty body.</param>
/// <param name="Failure">The exception that the request throws in place of an answer, or
/// <see langword="null"/> for a request that gets an answer.</param>
internal sealed record StubHttpAnswer(HttpStatusCode Status, string? Body = null, Exception? Failure = null)
{
    /// <summary>
    /// Creates an answer that makes the request throw an exception.
    /// </summary>
    /// <param name="failure">The exception.</param>
    /// <returns>The answer.</returns>
    public static StubHttpAnswer Throwing(Exception failure) => new(default, null, failure);
}
