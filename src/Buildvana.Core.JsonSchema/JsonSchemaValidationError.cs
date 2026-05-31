// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.JsonSchema;

/// <summary>
/// Describes a single JSON Schema validation failure.
/// </summary>
/// <param name="Kind">The kind of failure, for callers that map errors to their own diagnostics.</param>
/// <param name="JsonPointer">
/// An RFC 6901 JSON Pointer locating the offending value, or an empty string for the document root. This is the
/// stable, unambiguous key used to look the value up in a <see cref="JsonSourceMap"/>.
/// </param>
/// <param name="DisplayPath">
/// A human-friendly path to the offending value, or an empty string for the document root. Built during
/// validation, so object members render as <c>.name</c> and array elements as <c>[index]</c>, with a numeric
/// object key never mistaken for an index. Intended for display only; use <see cref="JsonPointer"/> as a key.
/// </param>
/// <param name="Message">A human-readable description of the failure.</param>
/// <param name="Line">The 1-based source line of the offending value, or 0 when it has not been resolved.</param>
/// <param name="Column">The 1-based source column of the offending value, or 0 when it has not been resolved.</param>
public sealed record JsonSchemaValidationError(
    JsonSchemaErrorKind Kind,
    string JsonPointer,
    string DisplayPath,
    string Message,
    int Line = 0,
    int Column = 0)
{
    /// <summary>
    /// Returns a string combining the display path and the message.
    /// </summary>
    /// <returns>The message alone for a root-level error, or <c>"{DisplayPath}: {Message}"</c> otherwise.</returns>
    public override string ToString()
        => DisplayPath.Length == 0 ? Message : $"{DisplayPath}: {Message}";
}
