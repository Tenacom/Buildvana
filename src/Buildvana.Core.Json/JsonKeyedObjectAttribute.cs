// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Core.Json;

/// <summary>
/// Marks a type as the element of a keyed-object list: a list whose JSON form is an object, with one property
/// per element, whose property names are data and whose property order carries meaning.
/// </summary>
/// <remarks>
/// <para>A property of type <see cref="System.Collections.Generic.IReadOnlyList{T}"/>, where <c>T</c> carries
/// this attribute, is (de)serialized as a JSON object by <see cref="JsonKeyedObjectConverter"/>: each JSON
/// property becomes one element, in document order, its name assigned to the element's key property.</para>
/// <para>When <paramref name="valuePropertyName"/> is given, each JSON property value is the value property's
/// value: the <c>{"key": value}</c> shape. When it is omitted, each JSON property value is an object holding
/// the element's remaining members: the <c>{"key": { ... }}</c> shape.</para>
/// </remarks>
/// <param name="keyPropertyName">
/// The CLR name of the element property that receives the JSON property name. The property must be of type
/// <see cref="string"/>. Use <c>nameof</c> to keep the reference refactor-safe.
/// </param>
/// <param name="valuePropertyName">
/// The CLR name of the element property that holds the JSON property value, or <see langword="null"/> for the
/// remaining-members shape. Use <c>nameof</c> to keep the reference refactor-safe.
/// </param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class JsonKeyedObjectAttribute(string keyPropertyName, string? valuePropertyName = null) : Attribute
{
    /// <summary>
    /// Gets the CLR name of the element property that receives the JSON property name.
    /// </summary>
    public string KeyPropertyName { get; } = keyPropertyName;

    /// <summary>
    /// Gets the CLR name of the element property that holds the JSON property value, or <see langword="null"/>
    /// if elements take the remaining-members shape.
    /// </summary>
    public string? ValuePropertyName { get; } = valuePropertyName;
}
