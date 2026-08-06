// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services;

/// <summary>
/// The per-target outcome of <see cref="SelfVersionService.UpdateRepositoryAsync"/>, one display line per
/// target, for the <c>update</c> command to print as its deliverable.
/// </summary>
/// <param name="ToolManifestLine">What happened to the bv pin in the tool manifest.</param>
/// <param name="GlobalJsonLine">What happened to the Buildvana SDK pin in <c>global.json</c>.</param>
/// <param name="ConfigFileLine">What happened to the configuration file's schema reference, or
/// <see langword="null"/> when the repository has no configuration file.</param>
internal sealed record UpdateSummary(
    string ToolManifestLine,
    string GlobalJsonLine,
    string? ConfigFileLine);
