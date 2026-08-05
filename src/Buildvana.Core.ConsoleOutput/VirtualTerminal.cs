// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Buildvana.Core.ConsoleOutput;

/// <summary>
/// Enables interpretation of ANSI (virtual terminal) escape sequences on the console.
/// </summary>
/// <remarks>
/// <para>POSIX terminals interpret escape sequences natively, as long as there is a capable terminal at all: an
/// unset, empty, or <c>dumb</c> <c>TERM</c> environment variable signals one that does not understand them. On
/// Windows, interpretation is a per-screen-buffer console mode (<c>ENABLE_VIRTUAL_TERMINAL_PROCESSING</c>) that
/// ConPTY-based hosts such as Windows Terminal enable by default but legacy conhost does not, so it must be
/// enabled explicitly before writing sequences from <see cref="AnsiEscapes"/>.</para>
/// <para>The screen buffer belongs to the console session, not to this process, so enabling the mode outlives
/// the process; it is deliberately not restored on exit, per common CLI practice.</para>
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Thin wrapper over Win32 console-mode APIs; behavior is owned by the console attached to the process, which a test cannot control (under a test runner standard error is redirected, so only the failure path would ever run).")]
public static partial class VirtualTerminal
{
    private const int StdErrorHandle = -12; // STD_ERROR_HANDLE
    private const uint EnableVirtualTerminalProcessing = 0x0004; // ENABLE_VIRTUAL_TERMINAL_PROCESSING

    /// <summary>
    /// Tries to ensure that the device attached to the process's standard error stream interprets ANSI escape
    /// sequences.
    /// </summary>
    /// <returns><see langword="true"/> if escape sequences written to standard error will be interpreted;
    /// <see langword="false"/> otherwise (including when standard error is not attached to a console).</returns>
    public static bool TryEnableOnStandardError()
    {
        if (!OperatingSystem.IsWindows())
        {
            // Interpretation is native, but only when a capable terminal is attached; TERM is the POSIX way to
            // declare one (Console.ForegroundColor consults full terminfo, of which this is the cheap proxy).
            var term = Environment.GetEnvironmentVariable("TERM");
            return !string.IsNullOrEmpty(term) && !string.Equals(term, "dumb", StringComparison.Ordinal);
        }

        var handle = GetStdHandle(StdErrorHandle);
        if (handle == IntPtr.Zero || handle == -1)
        {
            return false;
        }

        if (!GetConsoleMode(handle, out var mode))
        {
            return false;
        }

        if ((mode & EnableVirtualTerminalProcessing) != 0)
        {
            return true;
        }

        return SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);
    }

    // SetLastError = true documents the intended semantics of these imports: no caller examines
    // Marshal.GetLastWin32Error today, but capturing the last error keeps error codes available should console
    // misbehavior ever need diagnosing in the field.
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial IntPtr GetStdHandle(int nStdHandle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
}
