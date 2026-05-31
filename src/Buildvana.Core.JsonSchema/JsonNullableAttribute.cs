// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Core.JsonSchema;

/// <summary>
/// Marks a property whose JSON <see langword="null"/> is a meaningful value, so the schema generator keeps
/// <c>null</c> among the property's allowed types instead of stripping it.
/// </summary>
/// <remarks>
/// <para>Without this attribute, a nullable property is treated as merely optional: an absent key expresses
/// "unset", and an explicit <c>null</c> is disallowed by the generated schema.</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class JsonNullableAttribute : Attribute
{
}
