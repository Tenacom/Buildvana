// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Hosts.Forgejo;

/// <summary>
/// The members of a Forgejo API error answer that <c>bv</c> reads.
/// </summary>
internal sealed record ForgejoErrorBody
{
    /// <summary>
    /// Gets the error message, or <see langword="null"/> when the answer states none.
    /// </summary>
    public string? Message { get; init; }
}
