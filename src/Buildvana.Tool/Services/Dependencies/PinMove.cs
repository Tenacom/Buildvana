// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// A pin a run moves, with the version it moves to.
/// </summary>
/// <param name="Pin">The pin, as the repository states it.</param>
/// <param name="Target">The version the pin moves to.</param>
internal readonly record struct PinMove(DependencyPin Pin, NuGetVersion Target);
