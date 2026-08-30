// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel;
using Buildvana.Core.Json.Schema;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Models the extra arguments and environment variables for one kind of <c>dotnet</c> invocation,
/// as stated in a Buildvana configuration file.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record DotNetInvocationJsonConfig
{
    /// <summary>
    /// Gets extra arguments forwarded to <c>dotnet</c>.
    /// </summary>
    [JsonSchemaExample("""["--nologo"]""")]
    [Description("Extra arguments forwarded to `dotnet`. Not every command takes the same ones.")]
    public IReadOnlyList<string>? Args { get; init; }

    /// <summary>
    /// Gets environment variables forwarded to <c>dotnet</c>, keyed by variable name.
    /// </summary>
    [JsonSchemaExample("""{"NUGET_XMLDOC_MODE": "skip"}""")]
    [Description("Environment variables forwarded to `dotnet`, keyed by variable name.")]
    public IReadOnlyDictionary<string, string?>? Env { get; init; }
}
