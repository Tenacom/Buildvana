// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel;

namespace Buildvana.Core.Configuration;

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
