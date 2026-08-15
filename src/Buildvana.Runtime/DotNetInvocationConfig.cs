// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// The resolved extra arguments and environment variables for one kind of <c>dotnet</c> invocation.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record DotNetInvocationConfig
{
    /// <summary>
    /// Gets extra arguments passed to <c>dotnet</c>.
    /// </summary>
    public IReadOnlyList<string> Args { get; init; } = [];

    /// <summary>
    /// Gets environment variables set for <c>dotnet</c>, keyed by variable name.
    /// A <see langword="null"/> value removes the variable from the child environment.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Env { get; init; } = ReadOnlyDictionary<string, string?>.Empty;
}
