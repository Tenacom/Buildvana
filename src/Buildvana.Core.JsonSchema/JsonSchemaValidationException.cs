// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Buildvana.Core.JsonSchema;

/// <summary>
/// The exception thrown when a JSON value fails validation against a schema.
/// </summary>
public sealed class JsonSchemaValidationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonSchemaValidationException"/> class with no errors.
    /// </summary>
    public JsonSchemaValidationException()
        : this([])
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonSchemaValidationException"/> class with the specified message.
    /// </summary>
    /// <param name="message">A message describing the validation failure.</param>
    public JsonSchemaValidationException(string message)
        : base(message)
    {
        Errors = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonSchemaValidationException"/> class with the specified
    /// message and inner exception.
    /// </summary>
    /// <param name="message">A message describing the validation failure.</param>
    /// <param name="innerException">The exception that caused the current exception.</param>
    public JsonSchemaValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Errors = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonSchemaValidationException"/> class with the specified errors.
    /// </summary>
    /// <param name="errors">The validation errors that caused the failure.</param>
    public JsonSchemaValidationException(IReadOnlyList<JsonSchemaValidationError> errors)
        : base(FormatMessage(errors))
    {
        Errors = errors;
    }

    /// <summary>
    /// Gets the validation errors that caused the failure.
    /// </summary>
    public IReadOnlyList<JsonSchemaValidationError> Errors { get; }

    private static string FormatMessage(IReadOnlyList<JsonSchemaValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return errors.Count switch
        {
            0 => "JSON schema validation failed.",
            1 => $"JSON schema validation failed: {errors[0]}",
            _ => "JSON schema validation failed:" + Environment.NewLine
                + string.Join(Environment.NewLine, errors.Select(static error => $"  - {error}")),
        };
    }
}
