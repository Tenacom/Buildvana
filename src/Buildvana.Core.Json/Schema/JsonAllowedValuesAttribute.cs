// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Buildvana.Core.Json.Schema;

/// <summary>
/// Constrains a string-valued property to a fixed set of values, so the generated schema rejects any other
/// value and an editor offers the set as completions.
/// </summary>
/// <remarks>
/// <para>The values are matched exactly, as JSON Schema's <c>enum</c> keyword demands: a deserializer that
/// accepts a value in some other casing accepts more than the schema does, exactly as it does for an
/// enum-typed property.</para>
/// <para>An enumerated set already forbids the empty and all-whitespace strings, so a property carrying this
/// attribute states no <c>minLength</c> and no non-blank <c>pattern</c>, even when it is
/// <see langword="required"/>.</para>
/// </remarks>
/// <param name="values">
/// A comma-separated list of the values the property is allowed to hold, in schema-output order. Surrounding
/// whitespace is trimmed. A single string argument (rather than a <c>params</c> array) keeps the attribute
/// CLS-compliant.
/// </param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class JsonAllowedValuesAttribute(string values) : Attribute
{
    /// <summary>
    /// Gets the comma-separated values exactly as specified on the attribute.
    /// </summary>
    public string Values { get; } = values;

    /// <summary>
    /// Gets the individual values the property is allowed to hold, trimmed and in order.
    /// </summary>
    public IReadOnlyList<string> AllowedValues { get; } =
        values.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
