// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Hooks;

/// <summary>
/// What came of running a hook.
/// </summary>
/// <remarks>
/// <para>A hook that fails never produces one of these: the run stops with the exit code of a program
/// Buildvana invoked.</para>
/// </remarks>
internal enum HookOutcome
{
    /// <summary>The repository has no file for the hook, so nothing ran.</summary>
    NoHook,

    /// <summary>The hook ran and reported nothing to do.</summary>
    Completed,

    /// <summary>
    /// The hook of a check run reported that it would change something. The command folds that into its own
    /// verdict, as it folds a pin that has fallen behind.
    /// </summary>
    PendingWork,
}
