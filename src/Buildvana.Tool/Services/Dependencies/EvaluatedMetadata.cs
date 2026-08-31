// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Reads a metadatum of an evaluated item as the file that declares it means it.
/// </summary>
/// <remarks>
/// <para>MSBuild has no absent metadatum: one that was never stated evaluates to the empty string. A
/// metadatum a file states as a child element carries the element's own indentation into the evaluated
/// value, and that whitespace is the file's layout rather than the value.</para>
/// <para>Every reader of an evaluated item therefore reads its metadata through here, so that a file's
/// layout never reaches the rest of <c>bv</c>, and the readers of one repository agree on what a
/// declaration states.</para>
/// </remarks>
internal static class EvaluatedMetadata
{
    /// <summary>
    /// Reads what a metadatum states, with the whitespace around it removed.
    /// </summary>
    /// <param name="value">The evaluated value.</param>
    /// <returns>What the metadatum states, or <see langword="null"/> if it states nothing.</returns>
    public static string? Stated(string? value)
    {
        var trimmed = value?.Trim();
        return trimmed is { Length: > 0 } ? trimmed : null;
    }
}
