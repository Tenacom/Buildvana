// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Configures invocations of the <c>dotnet</c> CLI.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record DotNetConfig
{
    // Allowed keys for Args, in schema-output order. Shared between loader validation and schema generation
    // so the two cannot diverge.
    internal static readonly string[] AllowedArgsKeys = ["all", "restore", "build", "test", "pack"];

    /// <summary>Gets the default build configuration passed to <c>dotnet</c>.</summary>
    [Description("Default build configuration (e.g. Debug, Release).")]
    public string? Configuration { get; init; }

    /// <summary>Gets extra arguments forwarded to <c>dotnet</c>, keyed by command name (<c>all</c>, <c>restore</c>, <c>build</c>, <c>test</c>, <c>pack</c>).</summary>
    [Description("Extra arguments forwarded to dotnet, keyed by command name (all, restore, build, test, pack).")]
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? Args { get; init; }
}
