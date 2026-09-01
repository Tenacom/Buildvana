// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// One transitive override in effect, as the files on disk state it.
/// </summary>
/// <param name="PackageId">The package the entry is about.</param>
/// <param name="Version">The version the entry states, or <see langword="null"/> where it states none:
/// a promotion of a package whose version the central file, or a pin of the repository's own, supplies.</param>
/// <param name="DeclaringFile">The file stating the entry, relative to the home directory.</param>
/// <remarks>
/// <para>The version travels as text, as it is written. Nothing decides anything from these entries: they
/// are what <c>bv dependencies show</c> lists and what the <c>deps/post-update</c> hook is told.</para>
/// </remarks>
internal sealed record TransitiveOverrideEntry(string PackageId, string? Version, string DeclaringFile);
