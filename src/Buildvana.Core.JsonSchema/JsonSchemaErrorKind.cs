// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.JsonSchema;

/// <summary>
/// Identifies the kind of a <see cref="JsonSchemaValidationError"/>, so callers can map a failure to their own
/// diagnostics without parsing its message.
/// </summary>
public enum JsonSchemaErrorKind
{
    /// <summary>A value did not match the type the schema requires.</summary>
    TypeMismatch,

    /// <summary>A value was not among those allowed by an <c>enum</c> constraint.</summary>
    DisallowedValue,

    /// <summary>An object contained a property the schema does not allow.</summary>
    UnknownProperty,

    /// <summary>An object was missing a property the schema requires.</summary>
    MissingProperty,

    /// <summary>A value appeared where the schema allows none (a <c>false</c> schema).</summary>
    ValueNotAllowed,
}
