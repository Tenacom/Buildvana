// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Core.Json.Schema;

/// <summary>
/// Excludes a property from <c>default</c> emission when its schema is generated with a defaults instance
/// (see <see cref="JsonSchemaGenerator"/>). Meant for settings whose effective default is dynamic —
/// computed from other settings at run time — where the static value found on the defaults instance would
/// mislead.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class JsonSchemaNoDefaultAttribute : Attribute;
