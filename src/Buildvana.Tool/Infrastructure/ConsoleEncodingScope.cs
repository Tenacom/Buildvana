// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security;
using System.Text;

namespace Buildvana.Tool.Infrastructure;

/// <summary>
/// Sets the console's output and input encoding to UTF-8 for the lifetime of the scope, restoring the previous
/// encodings when it is disposed.
/// </summary>
/// <remarks>
/// <para>This mirrors what the .NET CLI does at startup (<c>src/Cli/dotnet/Program.cs</c> and
/// <c>UILanguageOverride.Setup()</c>, undone by its <c>AutomaticEncodingRestorer</c>; MSBuild does the same in
/// <c>XMake.cs</c>), and the mirroring is the point rather than a coincidence: <c>bv</c> is a component of a .NET
/// toolchain, so a console configured to suit <c>dotnet build</c> must suit <c>bv build</c> identically — down to
/// the same opt-out variable. See issue #339 for the research and the decision.</para>
/// <para>Without this, what <c>bv</c> can render would depend on how it was launched: reached through the muxer
/// (<c>dotnet bv</c>), the CLI has already switched the console to UTF-8 before <see cref="Program"/> runs; run as
/// a globally-installed native shim, nothing has, and characters outside the console's codepage are silently
/// best-fit mapped to lookalikes.</para>
/// <para>Both encodings are set, not just the output one: setting the pair is what moves the console's active
/// codepage, and MSBuild documents the consequence of setting output alone — "the encoding will work in CMD but
/// not in Powershell, as the raw CHCP page won't be changed". <c>bv</c> itself never reads console input.</para>
/// <para>The .NET CLI gates the pair on a non-English UI culture, because its switch exists to render localized
/// CLI output. Buildvana is single-culture English, so that premise is absent here, while the codepage reason
/// above is language-independent; copying the gate would leave English consoles behaving differently from every
/// other one, which is the unevenness this type exists to remove. The opt-out variable and the Windows build
/// floor, on the other hand, are the CLI's and are honored on its terms.</para>
/// <para>Restoration is best-effort, by the standard the CLI itself accepts: a normal exit and Ctrl-C (which
/// <see cref="Program"/> turns into an orderly shutdown) both restore the previous encodings; a hard kill does
/// not. The console is shared rather than owned, so a child process that changes it in turn — every <c>dotnet</c>
/// this one spawns does — captures and restores UTF-8, while this scope, being outermost, restores what the user
/// had.</para>
/// <para>Replacing the output encoding flushes and discards the console's cached <see cref="Console.Out"/> and
/// <see cref="Console.Error"/>, and restoring it does so a second time — by which point <c>ConsoleReporter</c>
/// has long been built. Nothing is left holding a writer over a stream that has moved on, because that reporter
/// reads the two properties at every write instead of capturing them at construction. That is its own decision,
/// documented on its own class; this one depends on it.</para>
/// </remarks>
internal sealed class ConsoleEncodingScope : IDisposable
{
    // Not Encoding.UTF8: what the console is set to is also what Console.OutputEncoding hands back afterwards,
    // because the setter stores a clone of it, and the BOM-emitting singleton would make every later reader of
    // that property inherit a preamble. Console.Out and Console.In are insulated either way — the console builds
    // them with the preamble stripped and BOM detection off — but nothing insulates a caller who writes, say,
    // `new StreamWriter(stream, Console.OutputEncoding)`. The two upstreams this class follows disagree here
    // (the .NET CLI passes Encoding.UTF8, MSBuild passes a BOM-less encoding), so there is no single toolchain
    // answer to conform to, and the choice falls to whichever leaves the smaller trap. Nothing in bv reads the
    // property today; this is about what it means when something does.
    // Internal rather than private, and pinned by a test, for the same reason as IsConsoleEncodingFailure below:
    // reverting to Encoding.UTF8 would compile, would change nothing anyone can see on the console, and would
    // quietly put the preamble back.
    internal static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    // The .NET CLI's own opt-out, honored here so that a single variable governs the whole toolchain.
    private const string UseDefaultEncodingVariable = "DOTNET_CLI_CONSOLE_USE_DEFAULT_ENCODING";

    private readonly Encoding? _originalOutputEncoding;
    private readonly Encoding? _originalInputEncoding;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleEncodingScope"/> class, replacing the console's
    /// encodings with UTF-8 unless the environment or the platform says otherwise.
    /// </summary>
    [ExcludeFromCodeCoverage(
        Justification = "Replaces the process console's encoding, which the test host owns; the opt-out it consults is tested separately.")]
    public ConsoleEncodingScope()
    {
        var optOutRequested = IsDefaultEncodingRequested(Environment.GetEnvironmentVariable(UseDefaultEncodingVariable));
        if (optOutRequested || !IsConsoleHostSafeForUtf8())
        {
            return;
        }

        // Output and input are handled independently: where console input is unavailable, reading its encoding
        // throws while the output encoding works fine, and half the job is still worth doing. A field ends up
        // non-null only if this scope actually replaced that encoding, so disposal puts back exactly what it
        // changed and nothing else.
        _originalOutputEncoding = ReplaceWithUtf8(
            static () => Console.OutputEncoding,
            static encoding => Console.OutputEncoding = encoding);
        _originalInputEncoding = ReplaceWithUtf8(
            static () => Console.InputEncoding,
            static encoding => Console.InputEncoding = encoding);
    }

    /// <inheritdoc/>
    [ExcludeFromCodeCoverage(Justification = "Restores the process console's encoding, which the test host owns.")]
    public void Dispose()
    {
        Restore(_originalOutputEncoding, static encoding => Console.OutputEncoding = encoding);
        Restore(_originalInputEncoding, static encoding => Console.InputEncoding = encoding);
    }

    /// <summary>
    /// Determines whether the value of <c>DOTNET_CLI_CONSOLE_USE_DEFAULT_ENCODING</c> asks for the console's
    /// encoding to be left alone.
    /// </summary>
    /// <param name="variableValue">The value of the environment variable, or <see langword="null"/> if unset.</param>
    /// <returns><see langword="true"/> if the console encoding must not be changed.</returns>
    /// <remarks>
    /// <para>Mirrors the .NET CLI exactly, including its test for the literal value <c>1</c> rather than for mere
    /// presence. <c>ConsoleReporter</c>'s <c>NO_COLOR</c> check treats any non-empty value as set: the two
    /// conventions differ because they have different owners, and each is honored on its owner's terms. They are
    /// not to be harmonized.</para>
    /// </remarks>
    internal static bool IsDefaultEncodingRequested(string? variableValue)
        => string.Equals(variableValue, "1", StringComparison.Ordinal);

    // The exceptions that reading or replacing a console encoding raises when the console cannot be
    // reconfigured, as opposed to when the call itself is wrong:
    //   - IOException — no console attached, or one that cannot be reconfigured; also what ConsolePal maps most
    //     Win32 failures to;
    //   - UnauthorizedAccessException — what it maps ERROR_ACCESS_DENIED to, absent from the documented list of
    //     what these setters throw, and reachable regardless;
    //   - SecurityException — the documented failure for a caller without the permission;
    //   - OperationCanceledException — what it maps ERROR_OPERATION_ABORTED to, and a cancellation in name only:
    //     no token reaches this class, whose guarded calls read one property and write another, so there is
    //     nobody whose cancellation this could be. Letting it through would be the worse outcome at either end —
    //     the constructor runs before Program's try block and Dispose after its catch blocks, so an escape is an
    //     unhandled crash over a cosmetic concern, and inside that try it would map to exit code 130, reporting
    //     a Ctrl-C nobody pressed;
    //   - NotSupportedException — a platform with no console encoding APIs at all, which throws
    //     PlatformNotSupportedException. This single clause is the whole of the handling for those, in place of
    //     the .NET CLI's enumeration of them: a copy of that list would be a snapshot to keep in step with an
    //     upstream we do not track, whereas catching what they throw covers platforms added later at no cost.
    // IOException, UnauthorizedAccessException and OperationCanceledException are total over the failure leg they
    // come from, rather than a sample of what has been seen there: ConsolePal routes every SetConsoleCP /
    // SetConsoleOutputCP failure it does not swallow outright through Win32Marshal.GetExceptionForWin32Error, and
    // every arm of that table produces one of the three. It passes no path, which narrows two arms that would
    // otherwise be path-dependent to plain IOException. The remaining two clauses cover what reaches here from
    // outside that table; Unix has its own ConsolePal, which does not use it at all.
    // Deliberately narrower than Exception.IsIORelatedException, which classifies failures of an I/O operation
    // and therefore admits ArgumentException to account for malformed paths. No arm of the table above produces
    // one, and no path reaches this class, so an ArgumentException here could only mean it handed the console
    // something the console cannot accept — a bug, and one that must not be swallowed.
    // Internal rather than private, and pinned by tests: the classification above is this class's one decision
    // that a reader could plausibly get wrong while leaving it compiling, and nothing else exercises it.
    internal static bool IsConsoleEncodingFailure(Exception exception)
        => exception is IOException
            or UnauthorizedAccessException
            or SecurityException
            or OperationCanceledException
            or NotSupportedException;

    // The build number is the .NET CLI's, from UILanguageOverride.OperatingSystemSupportsUtf8: below Windows 10
    // 1909 the console host is old enough for codepage 65001 to misbehave. No try/catch can stand in for this
    // check, because setting the encoding on those machines succeeds — the hazard is a destabilized console host,
    // not a failed call — and the number needs no keeping in step with future SDKs, since the build it names went
    // out of support in 2022 and nothing below it can run this tool anyway.
    // The CLI's guard also enumerates the platforms that have no console encoding APIs. That list is not repeated
    // here: ReplaceWithUtf8 catches what those platforms throw, which needs no maintenance and covers any added
    // after this was written.
    [ExcludeFromCodeCoverage(Justification = "The result is a property of the operating system the tests happen to run on.")]
    private static bool IsConsoleHostSafeForUtf8()
        => !OperatingSystem.IsWindows() || OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18363);

    [ExcludeFromCodeCoverage(Justification = "Reads and replaces the process console's encoding, which the test host owns and redirects.")]
    private static Encoding? ReplaceWithUtf8(Func<Encoding> get, Action<Encoding> set)
    {
        try
        {
            var original = get();
            set(Utf8NoBom);
            return original;
        }
        catch (Exception e) when (IsConsoleEncodingFailure(e))
        {
            // No console, no permission to reconfigure the one there is, or no console encoding APIs at all.
            // Whichever it is, the encoding stays as it was, and the null keeps Dispose from restoring an
            // encoding this scope never replaced.
            return null;
        }
    }

    [ExcludeFromCodeCoverage(Justification = "Writes the process console's encoding, which the test host owns and redirects.")]
    private static void Restore(Encoding? original, Action<Encoding> set)
    {
        if (original is null)
        {
            return;
        }

        try
        {
            set(original);
        }
        catch (Exception e) when (IsConsoleEncodingFailure(e))
        {
            // The console this scope changed is no longer there, or no longer ours to change back. Nothing
            // useful to do about it at exit.
        }
    }
}
