// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Core.ConsoleOutput;

/// <summary>
/// Represents an open activity: a "begin → detail → outcome" grouping of related work started by
/// <see cref="IReporter.BeginActivity(string)"/>.
/// </summary>
/// <remarks>
/// Use with a <c>using</c> statement and call <see cref="Complete"/> as the last statement of the block. The
/// outcome line is reported only when <see cref="Complete"/> ran before disposal; if the scope is disposed
/// without completing (for example because the work threw), nothing is reported — much like a transaction that
/// is rolled back when not committed.
/// </remarks>
public interface IActivityScope : IDisposable
{
    /// <summary>
    /// Marks the activity as successfully completed, so that disposing the scope reports its outcome.
    /// </summary>
    /// <param name="outcomeMessage">An optional message describing the outcome of the activity.</param>
    void Complete(string? outcomeMessage = null);
}
