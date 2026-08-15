// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// The configuration overrides stated on a run's command line: one nullable member per setting that a
/// command-line flag can override.
/// </summary>
/// <remarks>
/// <para>Like a wire model, this type is faithful to its source: <see langword="null"/> always means "not
/// stated on the command line", and no member carries a default. <see cref="BuildvanaConfigFactory"/> composes
/// these overrides, at the highest precedence, into the resolved domain model.</para>
/// </remarks>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record CommandLineOverrides
{
    /// <summary>
    /// Gets the build configuration stated on the command line. Overrides both <c>dotnet.configuration</c>
    /// and <c>release.configuration</c>.
    /// </summary>
    public string? Configuration { get; init; }

    /// <summary>Gets the public-API-check override (<c>--check-public-api</c>).</summary>
    public bool? CheckPublicApi { get; init; }

    /// <summary>Gets the dogfooding override (<c>--dogfood</c>).</summary>
    public bool? Dogfood { get; init; }

    /// <summary>
    /// Gets the arguments forwarded after the <c>--</c> separator, or <see langword="null"/> when none were.
    /// The factory appends them to the resolved arguments of the pipeline commands (<c>restore</c>,
    /// <c>build</c>, <c>test</c>, <c>pack</c>), never to <c>dotnet nuget push</c>.
    /// </summary>
    public IReadOnlyList<string>? ForwardedArgs { get; init; }
}
