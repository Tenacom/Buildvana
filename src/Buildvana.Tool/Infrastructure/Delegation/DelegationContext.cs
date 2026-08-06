// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Buildvana.Tool.Infrastructure.Delegation;

/// <summary>
/// Everything <see cref="DelegationService.TryDelegateAsync"/> needs to know about the current invocation in
/// order to decide whether to delegate it. Assembled by <c>Program</c> from the raw command line, the process
/// environment, and <c>AppContext.BaseDirectory</c>; tests supply synthetic values instead.
/// </summary>
/// <param name="RawArgs">The raw command-line arguments, exactly as received by <c>Main</c>. Forwarded verbatim
/// to the delegated bv, which parses and validates them itself (they may be valid for its version and not for
/// this one, or vice versa).</param>
/// <param name="Subcommand">The subcommand token, as split from the command line, or <see langword="null"/> if
/// there is none. Used only to exempt delegation-exempt subcommands; never validated.</param>
/// <param name="SkipDelegation">Whether <c>--skip-delegation</c> was passed: the user wants this exact binary
/// to run.</param>
/// <param name="DelegationMarkerPresent">Whether the <see cref="DelegationService.DelegatedEnvVar"/> environment
/// variable is set: this bv already is the target of a delegation, and must never delegate again.</param>
/// <param name="InstallLayout">The installation layout this bv executes from.</param>
/// <param name="StartDirectory">The directory home-directory discovery starts from, typically the current
/// working directory.</param>
internal sealed record DelegationContext(
    IReadOnlyList<string> RawArgs,
    string? Subcommand,
    bool SkipDelegation,
    bool DelegationMarkerPresent,
    InstallLayout InstallLayout,
    string StartDirectory);
