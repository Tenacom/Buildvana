// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Buildvana.Tool.Cli;

/// <summary>
/// Singleton holder for the arguments the pre-parser stashed for a <see cref="ConsumeAllArgumentsAttribute"/>
/// command to forward verbatim to its underlying <c>dotnet</c> invocation(s).
/// </summary>
internal sealed class ForwardedArguments
{
    /// <summary>
    /// Gets the forwarded arguments, in encounter order. Empty for commands that do not forward.
    /// </summary>
    public IReadOnlyList<string> Args { get; init; } = [];
}
