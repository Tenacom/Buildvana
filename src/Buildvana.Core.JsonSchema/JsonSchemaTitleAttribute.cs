// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Core.JsonSchema;

/// <summary>
/// Specifies the <c>title</c> keyword for the schema generated from the annotated type.
/// </summary>
/// <param name="title">The schema title.</param>
[AttributeUsage(AttributeTargets.Class)]
public sealed class JsonSchemaTitleAttribute(string title) : Attribute
{
    /// <summary>
    /// Gets the schema title.
    /// </summary>
    public string Title { get; } = title;
}
