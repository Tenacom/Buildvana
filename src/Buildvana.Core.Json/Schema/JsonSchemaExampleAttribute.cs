// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Core.Json.Schema;

/// <summary>
/// Supplies the <c>examples</c> keyword of the schema generated for the annotated property, so an editor can
/// offer a representative value and a generated document can state one.
/// </summary>
/// <remarks>
/// <para>The example is carried as a JSON fragment rather than as a typed value, because an attribute argument
/// must be a constant and the example of an array-valued or object-valued property cannot be one. The fragment
/// is parsed when the schema is generated, and an unparseable one fails generation.</para>
/// <para>On the key property of a <see cref="JsonKeyedObjectAttribute"/> element type, the example describes
/// the member name: it lands in the keyed object's <c>propertyNames</c>. A
/// <see cref="System.ComponentModel.DescriptionAttribute"/> on the same property travels the same route, and
/// for the same reason. On the value property the example describes the member value, and lands in
/// <c>additionalProperties</c>.</para>
/// </remarks>
/// <param name="json">The example value, written as a JSON fragment.</param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class JsonSchemaExampleAttribute(string json) : Attribute
{
    /// <summary>
    /// Gets the example value, as the JSON fragment specified on the attribute.
    /// </summary>
    public string Json { get; } = json;
}
