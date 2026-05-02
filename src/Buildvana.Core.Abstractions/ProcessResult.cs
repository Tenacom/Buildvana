// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Core;

/// <summary>
/// The outcome of a process invocation performed through <see cref="IProcessRunner"/>.
/// </summary>
/// <param name="ExitCode">The exit code reported by the process.</param>
/// <param name="StandardOutput">The full text written to the process's standard output stream.</param>
/// <param name="StandardError">The full text written to the process's standard error stream.</param>
/// <param name="Elapsed">The wall-clock time the process took to run.</param>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError, TimeSpan Elapsed);
