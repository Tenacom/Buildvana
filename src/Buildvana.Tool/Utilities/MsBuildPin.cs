// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Utilities;

/// <summary>
/// A package pin found in an MSBuild-syntax file: an item element of a well-known type, an identity, and the
/// raw text of its <c>Version</c> value.
/// </summary>
/// <param name="ItemType">The item element's name, as spelled in the file (e.g. <c>PackageVersion</c>).</param>
/// <param name="Id">The package id, from the item's <c>Include</c> attribute, as spelled in the file.
/// NuGet package ids are case-insensitive; compare accordingly.</param>
/// <param name="VersionText">The raw text of the item's <c>Version</c> value, whether attribute or child
/// element. Not guaranteed to parse as a version: it may be a property reference (<c>$(...)</c>), a range,
/// or a floating version, and deciding what to do with those forms is the caller's business. The text of
/// a child element is taken whole: whitespace around the version, which MSBuild does not trim either,
/// stays part of the value.</param>
// ReSharper disable once NotAccessedPositionalProperty.Global // First direct reader is the bv self-update family stamp (next PR)
internal sealed record MsBuildPin(string ItemType, string Id, string VersionText);
