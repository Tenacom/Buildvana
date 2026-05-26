// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

namespace Buildvana.Tool.Infrastructure.Execution;

/// <summary>
/// A dispatchable <c>bv</c> command. Implementing classes are decorated with <see cref="ImplementsCommandAttribute"/>,
/// discovered by <see cref="CommandRegistry"/>, and resolved from the DI container by <c>Program</c>.
/// </summary>
internal interface IBvCommand
{
    /// <summary>
    /// Executes the command.
    /// </summary>
    /// <param name="cancellationToken">A token cancelled when the user requests cancellation (e.g. Ctrl-C).</param>
    /// <returns>A task that resolves to the process exit code.</returns>
    Task<int> ExecuteAsync(CancellationToken cancellationToken);
}
