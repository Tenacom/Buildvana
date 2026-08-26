// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuGet.Versioning;

namespace Buildvana.Tool.Services;

/// <summary>
/// What a repository's .NET tool manifest says about bv, as read by <see cref="ToolManifest.ReadBvPin"/>.
/// </summary>
/// <remarks>
/// The three states the members encode — no entry, an entry without a usable version, and a usable pin — matter
/// to consumers in different ways: delegation treats anything without a usable <see cref="Version"/> as "no pin",
/// while <c>bv self-update</c> must distinguish a missing entry (which <c>dotnet tool install</c> creates) from an
/// unusable one (which no dotnet CLI verb can rewrite, since the CLI cannot parse the manifest either).
/// </remarks>
/// <param name="HasEntry">Whether the manifest has a bv entry at all, usable or not.</param>
/// <param name="VersionText">The entry's raw version text, or <see langword="null"/> when there is no entry or
/// the entry has no version string.</param>
/// <param name="Version">The pinned version, or <see langword="null"/> when <paramref name="VersionText"/> is
/// missing or does not parse as a version.</param>
internal sealed record BvManifestPin(bool HasEntry, string? VersionText, NuGetVersion? Version);
