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
/// other one, which is the unevenness this type exists to remove. The platform and Windows-version guards, on the
/// other hand, are copied verbatim.</para>
/// <para>Restoration is best-effort, by the standard the CLI itself accepts: a normal exit and Ctrl-C (which
/// <see cref="Program"/> turns into an orderly shutdown) both restore the previous encodings; a hard kill does
/// not. The console is shared rather than owned, so a child process that changes it in turn — every <c>dotnet</c>
/// this one spawns does — captures and restores UTF-8, while this scope, being outermost, restores what the user
/// had.</para>
/// </remarks>
internal sealed class ConsoleEncodingScope : IDisposable
{
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
        if (optOutRequested || !OperatingSystemSupportsUtf8())
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

    // Copied verbatim from the .NET CLI's UILanguageOverride.OperatingSystemSupportsUtf8(). The first four
    // exclude platforms where the console encoding APIs do not exist: unreachable for a command-line tool, free
    // at run time (the JIT folds them to constants), and kept so that this stays a copy rather than a copy minus
    // some bits that the next resync would have to re-justify. The Windows build floor is the load-bearing one,
    // and it cannot be traded for a try/catch: below that build, setting the encoding succeeds — the hazard it
    // guards against is a destabilized console host, not an exception.
    [ExcludeFromCodeCoverage(Justification = "The result is a property of the operating system the tests happen to run on.")]
    private static bool OperatingSystemSupportsUtf8()
        => !OperatingSystem.IsIOS()
            && !OperatingSystem.IsAndroid()
            && !OperatingSystem.IsTvOS()
            && !OperatingSystem.IsBrowser()
            && (!OperatingSystem.IsWindows() || OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18363));

    [ExcludeFromCodeCoverage(Justification = "Reads and replaces the process console's encoding, which the test host owns and redirects.")]
    private static Encoding? ReplaceWithUtf8(Func<Encoding> get, Action<Encoding> set)
    {
        try
        {
            var original = get();
            set(Encoding.UTF8);
            return original;
        }
        catch (IOException)
        {
            // No console is attached, or it cannot be reconfigured. Leave the encoding as it is.
            return null;
        }
        catch (SecurityException)
        {
            // Likewise, for a caller without the permission to change it.
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
        catch (IOException)
        {
            // The console this scope changed is no longer there to change back; nothing useful to do at exit.
        }
        catch (SecurityException)
        {
            // Likewise, for a caller without the permission to change it.
        }
    }
}
