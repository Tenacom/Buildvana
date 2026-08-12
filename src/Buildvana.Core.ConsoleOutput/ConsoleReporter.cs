// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Buildvana.Core.ConsoleOutput;

/// <summary>
/// A <see cref="TextWriterReporter"/> that writes to the process's standard streams via <see cref="Console"/>.
/// </summary>
/// <remarks>
/// <para>This class is the whole of the reporter's coupling to the environment: it decides whether the console
/// can render color, and it names the standard streams as the writers to render to. Everything else — what a
/// rendered line looks like and when it is written at all — lives in <see cref="TextWriterReporter"/>, where it
/// can be tested without a console. Anything added here that would deserve a test belongs there instead.</para>
/// <para><see cref="Console.Out"/> and <see cref="Console.Error"/> are read at every write, not captured at
/// construction, so a later <see cref="Console.SetOut"/> or <see cref="Console.SetError"/> is honored. That is
/// the obvious reason rather than the load-bearing one, which no caller in this repository makes visible from
/// here: replacing <see cref="Console.OutputEncoding"/> also flushes and discards both cached writers, so a
/// reporter that had captured them would go on writing to streams the console has abandoned. <c>bv</c> does
/// exactly that twice per run — once at startup and once on the way out, the second time long after this
/// reporter is built — so caching the two writers here would break it, silently and at a distance.</para>
/// </remarks>
[ExcludeFromCodeCoverage(
    Justification = "Every member reads or manipulates the process console, whose state a test runner owns and redirects.")]
public sealed class ConsoleReporter : TextWriterReporter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleReporter"/> class.
    /// </summary>
    /// <param name="verbosity">The verbosity that gates which message levels are rendered.</param>
    /// <param name="colorOverride">
    /// <see langword="true"/> to force color on, <see langword="false"/> to force it off, or
    /// <see langword="null"/> to auto-detect (color on unless standard error is redirected, the <c>NO_COLOR</c>
    /// environment variable is set, or the console cannot interpret ANSI escape sequences). A
    /// non-<see langword="null"/> value wins over all three, so <c>--color</c> overrides <c>NO_COLOR</c>.
    /// Forcing color on also attempts to enable escape-sequence interpretation on the console, but wins even
    /// when that fails.
    /// </param>
    public ConsoleReporter(Verbosity verbosity, bool? colorOverride)
        : base(verbosity, ResolveColor(colorOverride))
    {
    }

    /// <inheritdoc/>
    protected override TextWriter OutputWriter => Console.Out;

    /// <inheritdoc/>
    protected override TextWriter ErrorWriter => Console.Error;

    private static bool ResolveColor(bool? colorOverride)
    {
        if (colorOverride is not { } forced)
        {
            return DetectColor();
        }

        // Auto-detection folds the virtual-terminal attempt into DetectColor; an explicit "on" override skips
        // detection, so the attempt must happen here or legacy conhost would print raw escape sequences. The
        // override stays authoritative even when enabling fails: the user asked for color.
        if (forced)
        {
            _ = VirtualTerminal.TryEnableOnStandardError();
        }

        return forced;
    }

    private static bool DetectColor()
        => !IsNoColorSet() && !Console.IsErrorRedirected && VirtualTerminal.TryEnableOnStandardError();

    // The NO_COLOR environment variable is a widely-adopted convention for opting out of color in command-line
    // applications. See https://no-color.org for more information.
    private static bool IsNoColorSet() => Environment.GetEnvironmentVariable("NO_COLOR") is { Length: > 0 };
}
