// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// What the lifecycle decided about one vulnerable package of one project.
/// </summary>
/// <param name="Outcome">What is to be done.</param>
/// <param name="Version">The version to write, stated only by <see cref="OverrideOutcome.Override"/>.</param>
/// <param name="Reason">Why nothing is to be done, stated only by <see cref="OverrideOutcome.NoFix"/> and
/// <see cref="OverrideOutcome.Blocked"/>. It is a sentence, and the caller names the package before it.</param>
internal sealed record OverrideDecision(OverrideOutcome Outcome, NuGetVersion? Version, string? Reason);
