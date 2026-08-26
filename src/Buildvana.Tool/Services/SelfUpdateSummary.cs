// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Buildvana.Tool.Services;

/// <summary>
/// The per-target outcome of <see cref="SelfVersionService.UpdateRepositoryAsync"/>, one display line per
/// target, for the <c>self-update</c> command to print as its deliverable.
/// </summary>
/// <param name="ToolManifestLine">What happened to the bv pin in the tool manifest.</param>
/// <param name="GlobalJsonLine">What happened to the Buildvana SDK pin in <c>global.json</c>.</param>
/// <param name="FamilyPinLines">What happened to each family pin declared in the repository's own files —
/// one line per pin found, the unchanged and left-alone ones included, so the user can check that every
/// intended pin was discovered. Empty when there is none.</param>
/// <param name="ConfigFileLine">What happened to the configuration file's schema reference, or
/// <see langword="null"/> when the repository has no configuration file.</param>
internal sealed record SelfUpdateSummary(
    string ToolManifestLine,
    string GlobalJsonLine,
    IReadOnlyList<string> FamilyPinLines,
    string? ConfigFileLine);
