// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Build;

/// <summary>
/// A step in the build pipeline. Members are declared in execution order, so they can be compared and
/// iterated to run a contiguous range of steps.
/// </summary>
internal enum BuildStep
{
    Clean,
    Restore,
    Build,
    Test,
    Pack,
}
