// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuGet.Versioning;

namespace Buildvana.Tool.Utilities;

/// <summary>
/// The running bv's own version, parsed once from the assembly's informational version.
/// </summary>
internal static class OwnVersion
{
    /// <summary>
    /// Gets the running bv's version. The informational version's build metadata (e.g. <c>+g0123abc</c>) is
    /// carried through the parse but never participates in comparisons; pins and messages use
    /// <see cref="NuGetVersion.ToNormalizedString"/>, which drops it.
    /// </summary>
    public static NuGetVersion Value { get; } = NuGetVersion.Parse(ThisAssembly.AssemblyInformationalVersion);
}
