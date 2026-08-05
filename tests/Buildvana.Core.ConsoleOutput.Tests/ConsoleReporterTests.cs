// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.ConsoleOutput;

// Tests swap the process-global console writers, so they must not run concurrently with each other.
[NotInParallel]
internal sealed class ConsoleReporterTests
{
    [Test]
    [Arguments(MessageLevel.Error, "error")]
    [Arguments(MessageLevel.Warning, "warning")]
    [Arguments(MessageLevel.Info, "info")]
    [Arguments(MessageLevel.Detail, "detail")]
    [Arguments(MessageLevel.Trace, "trace")]
    public async Task Report_EnabledLevel_WritesLabeledLineToStandardError(MessageLevel level, string label)
    {
        var reporter = new ConsoleReporter(Verbosity.Diagnostic, colorOverride: false);
        var (stdout, stderr) = CaptureConsole(() => reporter.Report(level, "something happened"));
        await Assert.That(stderr).IsEqualTo($"{label}: something happened{Environment.NewLine}");
        await Assert.That(stdout).IsEmpty();
    }

    [Test]
    [Arguments(Verbosity.Quiet, MessageLevel.Warning)]
    [Arguments(Verbosity.Minimal, MessageLevel.Info)]
    [Arguments(Verbosity.Normal, MessageLevel.Detail)]
    [Arguments(Verbosity.Detailed, MessageLevel.Trace)]
    public async Task Report_DisabledLevel_WritesNothing(Verbosity verbosity, MessageLevel level)
    {
        var reporter = new ConsoleReporter(verbosity, colorOverride: false);
        var (stdout, stderr) = CaptureConsole(() => reporter.Report(level, "something happened"));
        await Assert.That(stderr).IsEmpty();
        await Assert.That(stdout).IsEmpty();
    }

    [Test]
    [Arguments(MessageLevel.Error, "\e[91m", "error")]
    [Arguments(MessageLevel.Warning, "\e[93m", "warning")]
    public async Task Report_ColoredLevelWithColorOn_WrapsOnlyLabelInEscapes(MessageLevel level, string escape, string label)
    {
        var reporter = new ConsoleReporter(Verbosity.Normal, colorOverride: true);
        var (stdout, stderr) = CaptureConsole(() => reporter.Report(level, "something happened"));
        await Assert.That(stderr).IsEqualTo($"{escape}{label}:\e[0m something happened{Environment.NewLine}");
        await Assert.That(stdout).IsEmpty();
    }

    [Test]
    public async Task Report_UncoloredLevelWithColorOn_WritesNoEscapes()
    {
        var reporter = new ConsoleReporter(Verbosity.Normal, colorOverride: true);
        var (stdout, stderr) = CaptureConsole(() => reporter.Info("something happened"));
        await Assert.That(stderr).IsEqualTo($"info: something happened{Environment.NewLine}");
        await Assert.That(stdout).IsEmpty();
    }

    [Test]
    public async Task BeginActivity_AtNormalVerbosity_WritesHeaderToStandardError()
    {
        var reporter = new ConsoleReporter(Verbosity.Normal, colorOverride: false);
        var (stdout, stderr) = CaptureConsole(() => reporter.BeginActivity("Doing stuff"));
        await Assert.That(stderr).IsEqualTo($"[1] Doing stuff: starting...{Environment.NewLine}");
        await Assert.That(stdout).IsEmpty();
    }

    [Test]
    public async Task BeginActivity_BelowNormalVerbosity_WritesNothing()
    {
        var reporter = new ConsoleReporter(Verbosity.Minimal, colorOverride: false);
        var (stdout, stderr) = CaptureConsole(() =>
        {
            using var scope = reporter.BeginActivity("Doing stuff");
            scope.Complete();
        });

        await Assert.That(stderr).IsEmpty();
        await Assert.That(stdout).IsEmpty();
    }

    [Test]
    public async Task BeginActivity_Nested_ReflectsDepthInHeaders()
    {
        var reporter = new ConsoleReporter(Verbosity.Normal, colorOverride: false);
        var (stdout, stderr) = CaptureConsole(() =>
        {
            using var outer = reporter.BeginActivity("Outer");
            using var inner = reporter.BeginActivity("Inner");
        });

        await Assert.That(stderr).Contains("[1] Outer: starting...");
        await Assert.That(stderr).Contains("[2] Inner: starting...");
        await Assert.That(stdout).IsEmpty();
    }

    [Test]
    public async Task ActivityScope_Completed_WritesOutcomeToStandardError()
    {
        var reporter = new ConsoleReporter(Verbosity.Normal, colorOverride: false);
        var (stdout, stderr) = CaptureConsole(() =>
        {
            using var scope = reporter.BeginActivity("Doing stuff");
            scope.Complete("all good");
        });

        await Assert.That(stderr).Contains("[1] Doing stuff: done (");
        await Assert.That(stderr).Contains(" - all good");
        await Assert.That(stdout).IsEmpty();
    }

    [Test]
    public async Task ActivityScope_NotCompleted_WritesNoOutcome()
    {
        var reporter = new ConsoleReporter(Verbosity.Normal, colorOverride: false);
        var (stdout, stderr) = CaptureConsole(() =>
        {
            using var scope = reporter.BeginActivity("Doing stuff");
        });

        await Assert.That(stderr).IsEqualTo($"[1] Doing stuff: starting...{Environment.NewLine}");
        await Assert.That(stdout).IsEmpty();
    }

    [Test]
    public async Task ChildOutput_WritesToStandardOutput()
    {
        var reporter = new ConsoleReporter(Verbosity.Normal, colorOverride: false);
        var (stdout, stderr) = CaptureConsole(() => reporter.ChildOutput("child stdout line", null));
        await Assert.That(stdout).IsEqualTo($"child stdout line{Environment.NewLine}");
        await Assert.That(stderr).IsEmpty();
    }

    [Test]
    public async Task ChildOutput_BelowMinimumVerbosity_WritesNothing()
    {
        var reporter = new ConsoleReporter(Verbosity.Minimal, colorOverride: false);
        var (stdout, stderr) = CaptureConsole(() => reporter.ChildOutput("child stdout line", Verbosity.Normal));
        await Assert.That(stdout).IsEmpty();
        await Assert.That(stderr).IsEmpty();
    }

    [Test]
    public async Task ChildOutput_NoMinimumVerbosity_IgnoresVerbosity()
    {
        var reporter = new ConsoleReporter(Verbosity.Quiet, colorOverride: false);
        var (stdout, stderr) = CaptureConsole(() => reporter.ChildOutput("child stdout line", null));
        await Assert.That(stdout).IsEqualTo($"child stdout line{Environment.NewLine}");
        await Assert.That(stderr).IsEmpty();
    }

    [Test]
    public async Task ChildError_WritesToStandardError()
    {
        var reporter = new ConsoleReporter(Verbosity.Normal, colorOverride: false);
        var (stdout, stderr) = CaptureConsole(() => reporter.ChildError("child stderr line", null));
        await Assert.That(stderr).IsEqualTo($"child stderr line{Environment.NewLine}");
        await Assert.That(stdout).IsEmpty();
    }

    [Test]
    public async Task ChildError_BelowMinimumVerbosity_WritesNothing()
    {
        var reporter = new ConsoleReporter(Verbosity.Minimal, colorOverride: false);
        var (stdout, stderr) = CaptureConsole(() => reporter.ChildError("child stderr line", Verbosity.Normal));
        await Assert.That(stdout).IsEmpty();
        await Assert.That(stderr).IsEmpty();
    }

    private static (string Stdout, string Stderr) CaptureConsole(Action action)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var outWriter = new StringWriter();
        using var errorWriter = new StringWriter();

        // The writers are restored in the finally block below, and [NotInParallel] on the class keeps other
        // tests from observing the swapped writers, so TUnit's own console capture is preserved.
#pragma warning disable TUnit0055 // Overwriting the Console writer can break TUnit logging
        Console.SetOut(outWriter);
        Console.SetError(errorWriter);
        try
        {
            action();
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
#pragma warning restore TUnit0055

        return (outWriter.ToString(), errorWriter.ToString());
    }
}
