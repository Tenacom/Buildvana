// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuGet.Versioning;

namespace Buildvana.Tool.Services;

/// <summary>
/// A family pin found by <see cref="FamilyPinUpdater"/> in one of the repository's own files: where it is
/// declared, its package id, and its version as written and as parsed.
/// </summary>
/// <param name="RelativePath">The declaring file's path, relative to the home directory, with <c>/</c> as the
/// separator.</param>
/// <param name="Id">The package id, as spelled at the declaring site.</param>
/// <param name="VersionText">The raw version text at the declaring site, surrounding whitespace included.</param>
/// <param name="Version">The parsed version, or <see langword="null"/> when the trimmed
/// <paramref name="VersionText"/> is not a literal version — a property reference, a range, a floating
/// version.</param>
internal sealed record FamilyPin(string RelativePath, string Id, string VersionText, NuGetVersion? Version);
