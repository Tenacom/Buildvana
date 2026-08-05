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
    [ExcludeFromCodeCoverage(Justification = "Environment-owned plumbing: reads TERM off Windows, manipulates the attached console on Windows. The decision logic is tested via IsNonWindowsTerminalCapable.")]
    public static bool TryEnableOnStandardError()
    {
        if (!OperatingSystem.IsWindows())
        {
            // Interpretation is native, but only when a capable terminal is attached; TERM is the POSIX way to
            // declare one (Console.ForegroundColor consults full terminfo, of which this is the cheap proxy).
            return IsNonWindowsTerminalCapable(Environment.GetEnvironmentVariable("TERM"));
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

    /// <summary>
    /// Determines whether a <c>TERM</c> environment variable value declares a terminal capable of interpreting
    /// escape sequences.
    /// </summary>
    /// <param name="term">The value of the <c>TERM</c> environment variable, or <see langword="null"/> if the
    /// variable is unset.</param>
    /// <returns><see langword="true"/> if <paramref name="term"/> declares a capable terminal;
    /// <see langword="false"/> if it is <see langword="null"/>, empty, or <c>dumb</c>.</returns>
    internal static bool IsNonWindowsTerminalCapable(string? term)
        => !string.IsNullOrEmpty(term) && !string.Equals(term, "dumb", StringComparison.Ordinal);

    // SetLastError = true documents the intended semantics of these imports: no caller examines
    // Marshal.GetLastWin32Error today, but capturing the last error keeps error codes available should console
    // misbehavior ever need diagnosing in the field. Attributes on these declarations merge into the
    // generator-emitted implementations, so the coverage exclusions below also remove the marshalling stubs
    // (never executed off Windows) from the coverage denominator.
    [ExcludeFromCodeCoverage(Justification = "P/Invoke wrapper; behavior is owned by the Win32 console subsystem.")]
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial IntPtr GetStdHandle(int nStdHandle);

    [ExcludeFromCodeCoverage(Justification = "P/Invoke wrapper; behavior is owned by the Win32 console subsystem.")]
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [ExcludeFromCodeCoverage(Justification = "P/Invoke wrapper; behavior is owned by the Win32 console subsystem.")]
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
}
