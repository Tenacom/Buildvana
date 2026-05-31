// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Buildvana.Core.JsonSchema;

/// <summary>
/// Constrains a dictionary-valued property to a fixed set of keys, so the generated schema rejects any other
/// key (and an editor flags it).
/// </summary>
/// <param name="keys">
/// A comma-separated list of the keys the dictionary is allowed to contain, in schema-output order. Surrounding
/// whitespace is trimmed. A single string argument (rather than a <c>params</c> array) keeps the attribute
/// CLS-compliant.
/// </param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class JsonAllowedKeysAttribute(string keys) : Attribute
{
    /// <summary>
    /// Gets the comma-separated keys exactly as specified on the attribute.
    /// </summary>
    public string Keys { get; } = keys;

    /// <summary>
    /// Gets the individual keys the dictionary is allowed to contain, trimmed and in order.
    /// </summary>
    public IReadOnlyList<string> AllowedKeys { get; } =
        keys.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
