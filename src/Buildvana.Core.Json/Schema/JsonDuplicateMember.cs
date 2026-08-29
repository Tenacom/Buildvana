// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.Json.Schema;

/// <summary>
/// Describes one repeat of an object member name within a JSON document.
/// </summary>
/// <param name="Name">The member name, repeated from an earlier member of the same object.</param>
/// <param name="JsonPointer">
/// An RFC 6901 JSON Pointer locating the member. Both occurrences share it, being the same member of the
/// same object; <see cref="Line"/> and <see cref="Column"/> tell them apart.
/// </param>
/// <param name="Line">The 1-based source line of the repeated name.</param>
/// <param name="Column">The 1-based source column of the repeated name, which is where its opening quote is.</param>
public sealed record JsonDuplicateMember(string Name, string JsonPointer, int Line, int Column);
