// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Configures the extra arguments and environment variables for one kind of <c>dotnet</c> invocation.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record DotNetInvocationConfig
{
    /// <summary>
    /// Gets extra arguments forwarded to <c>dotnet</c>.
    /// </summary>
    [Description("Extra arguments forwarded to `dotnet`.")]
    public IReadOnlyList<string>? Args { get; init; }

    /// <summary>
    /// Gets environment variables forwarded to <c>dotnet</c>, keyed by variable name.
    /// </summary>
    [Description("Environment variables forwarded to `dotnet`, keyed by variable name.")]
    public IReadOnlyDictionary<string, string?>? Env { get; init; }
}
