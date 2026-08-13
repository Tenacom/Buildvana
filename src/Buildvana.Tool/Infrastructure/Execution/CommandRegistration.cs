// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Buildvana.Tool.Infrastructure.Execution;

/// <summary>
/// A discovered <c>bv</c> command: the paths it is registered under, the class that implements it, whether
/// it forwards all of its arguments verbatim, and its settings type (if any). Produced by
/// <see cref="CommandRegistry"/> from <see cref="ImplementsCommandAttribute"/>.
/// </summary>
/// <param name="AliasPaths">The paths the command is invoked under, each as a list of segments. The first path is canonical.</param>
/// <param name="CommandType">The class implementing the command.</param>
/// <param name="ConsumesAllArguments">Whether the command forwards all of its arguments verbatim.</param>
/// <param name="SettingsType">The command's <c>*Settings</c> type, or <see langword="null"/> if it has none.</param>
/// <param name="UsesSdk">Whether the command uses the repository's pinned Buildvana SDK and must therefore
/// pass the SDK version check before running.</param>
internal sealed record CommandRegistration(
    IReadOnlyList<IReadOnlyList<string>> AliasPaths,
    Type CommandType,
    bool ConsumesAllArguments,
    Type? SettingsType,
    bool UsesSdk = false)
{
    /// <summary>
    /// Gets the canonical path: the first alias, whose segments name the command in help and error messages.
    /// </summary>
    public IReadOnlyList<string> CanonicalPath => AliasPaths[0];

    /// <summary>
    /// Gets the canonical command name, space-joined as typed on the command line (e.g. <c>"version advance"</c>).
    /// </summary>
    public string Name => string.Join(' ', CanonicalPath);
}
