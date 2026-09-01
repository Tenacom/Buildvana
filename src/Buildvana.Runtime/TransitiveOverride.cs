// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// One transitive override in effect, as the generated files state it.
/// </summary>
/// <remarks>
/// <para>An override forces the resolved version of a package the repository does not reference directly.
/// <c>bv</c> writes one to lift a transitive dependency out of a version a security advisory covers, and
/// rewrites every one of them at each apply run that manages the <c>packages</c> scope.</para>
/// <para>These are the files as they stand when the hook runs, which a check run never touches.</para>
/// </remarks>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record TransitiveOverride
{
    /// <summary>Gets the id of the package the override is about.</summary>
    public required string PackageId { get; init; }

    /// <summary>
    /// Gets the version the override states, or <see langword="null"/> where it states none: a promotion of a
    /// package whose version the repository's own central pin supplies.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>Gets the generated file stating the override, relative to the home directory.</summary>
    public required string DeclaringFile { get; init; }
}
