// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.ConsoleOutput;

/// <summary>
/// An <see cref="IActivityScope"/> that does nothing. Returned by <see cref="NullReporter"/> and usable
/// anywhere a non-rendering activity scope is needed.
/// </summary>
public sealed class NullActivityScope : IActivityScope
{
    private NullActivityScope()
    {
    }

    /// <summary>
    /// Gets the singleton <see cref="NullActivityScope"/> instance.
    /// </summary>
    public static NullActivityScope Instance { get; } = new();

    /// <inheritdoc/>
    public void Complete(string? outcomeMessage = null)
    {
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}
