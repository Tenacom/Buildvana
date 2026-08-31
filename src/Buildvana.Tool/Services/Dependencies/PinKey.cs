// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// What tells one declaration of a pin from another inside one file.
/// </summary>
/// <param name="ItemType">The item type, upper-cased, or an empty string for a directive.</param>
/// <param name="Id">The package id, upper-cased.</param>
/// <param name="VersionText">The version text, with the whitespace around it removed.</param>
/// <remarks>
/// <para>Two declarations of one id in one file — conditioned per target framework, say — are told apart by
/// their version text, and two that state the same version are one pin.</para>
/// </remarks>
internal readonly record struct PinKey(string ItemType, string Id, string VersionText);
