// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Runtime;

/// <summary>
/// The exception thrown when Buildvana configuration or run-time information cannot be loaded.
/// </summary>
public sealed class BuildvanaRuntimeException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BuildvanaRuntimeException"/> class.
    /// </summary>
    public BuildvanaRuntimeException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildvanaRuntimeException"/> class with a message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public BuildvanaRuntimeException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildvanaRuntimeException"/> class with a message
    /// and a reference to the exception that caused it.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public BuildvanaRuntimeException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
