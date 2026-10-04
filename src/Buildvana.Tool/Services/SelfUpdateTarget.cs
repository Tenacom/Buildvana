// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuGet.Versioning;

namespace Buildvana.Tool.Services;

/// <summary>
/// The version <c>bv self-update</c> moves the repository to, as <see cref="SelfUpdateTargetResolver"/> picks it.
/// </summary>
/// <param name="Version">The target version.</param>
/// <param name="Description">A clause stating the version and where it came from, such as
/// <c>The version given with --to is 2.1.0</c>, for a message that has to say why the target is what it is.</param>
internal sealed record SelfUpdateTarget(NuGetVersion Version, string Description);
