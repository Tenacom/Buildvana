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
/// <para>POSIX terminals interpret escape sequences natively. On Windows, interpretation is a per-screen-buffer
/// console mode (<c>ENABLE_VIRTUAL_TERMINAL_PROCESSING</c>) that ConPTY-based hosts such as Windows Terminal
/// enable by default but legacy conhost does not, so it must be enabled explicitly before writing sequences
/// from <see cref="AnsiEscapes"/>.</para>
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
            return true;
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
