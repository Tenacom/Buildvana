// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Buildvana.Tool.Services;

/// <summary>
/// The resolved extra arguments and environment variables for one kind of <c>dotnet</c> invocation,
/// taken from a <see cref="Buildvana.Core.Configuration.DotNetInvocationConfig"/>.
/// </summary>
/// <param name="Args">Extra arguments appended to the invocation.</param>
/// <param name="Env">Environment variables applied on top of the inherited environment, keyed by variable name.</param>
internal sealed record DotNetInvocationSettings(
    IReadOnlyList<string> Args,
    IReadOnlyDictionary<string, string?> Env)
{
    /// <summary>
    /// Gets an empty set of invocation settings (no extra arguments, no environment variables).
    /// </summary>
    public static DotNetInvocationSettings Empty { get; } = new([], ReadOnlyDictionary<string, string?>.Empty);
}
