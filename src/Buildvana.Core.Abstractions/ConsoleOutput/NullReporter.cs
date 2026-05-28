// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.ConsoleOutput;

/// <summary>
/// An <see cref="IReporter"/> that discards everything. Useful as a default argument or in tests that do not
/// assert on output.
/// </summary>
public sealed class NullReporter : IReporter
{
    private NullReporter()
    {
    }

    /// <summary>
    /// Gets the singleton <see cref="NullReporter"/> instance.
    /// </summary>
    public static NullReporter Instance { get; } = new();

    /// <inheritdoc/>
    public Verbosity Verbosity => Verbosity.Quiet;

    /// <inheritdoc/>
    public void Report(MessageLevel level, string message)
    {
    }

    /// <inheritdoc/>
    public IActivityScope BeginActivity(string title) => NullActivityScope.Instance;

    /// <inheritdoc/>
    public void ChildOutput(string line)
    {
    }

    /// <inheritdoc/>
    public void ChildError(string line)
    {
    }
}
