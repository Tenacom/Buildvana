// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Testing;

internal sealed class TextWriterReporterTests
{
    [Test]
    [Arguments(MessageLevel.Error, "error")]
    [Arguments(MessageLevel.Warning, "warning")]
    [Arguments(MessageLevel.Notice, "notice")]
    [Arguments(MessageLevel.Info, "info")]
    [Arguments(MessageLevel.Detail, "detail")]
    [Arguments(MessageLevel.Trace, "trace")]
    public async Task Report_EnabledLevel_WritesLabeledLineToErrorWriter(MessageLevel level, string label)
    {
        using var reporter = new StringWriterReporter(Verbosity.Diagnostic);
        reporter.Report(level, "something happened");
        await Assert.That(reporter.ErrorText).IsEqualTo($"{label}: something happened{Environment.NewLine}");
        await Assert.That(reporter.OutputText).IsEmpty();
    }

    [Test]
    [Arguments(Verbosity.Quiet, MessageLevel.Warning)]
    [Arguments(Verbosity.Quiet, MessageLevel.Notice)]
    [Arguments(Verbosity.Minimal, MessageLevel.Info)]
    [Arguments(Verbosity.Normal, MessageLevel.Detail)]
    [Arguments(Verbosity.Detailed, MessageLevel.Trace)]
    public async Task Report_DisabledLevel_WritesNothing(Verbosity verbosity, MessageLevel level)
    {
        using var reporter = new StringWriterReporter(verbosity);
        reporter.Report(level, "something happened");
        await Assert.That(reporter.ErrorText).IsEmpty();
        await Assert.That(reporter.OutputText).IsEmpty();
    }

    [Test]
    [Arguments(MessageLevel.Error, Verbosity.Quiet)]
    [Arguments(MessageLevel.Warning, Verbosity.Minimal)]
    [Arguments(MessageLevel.Notice, Verbosity.Minimal)]
    [Arguments(MessageLevel.Info, Verbosity.Normal)]
    [Arguments(MessageLevel.Detail, Verbosity.Detailed)]
    [Arguments(MessageLevel.Trace, Verbosity.Diagnostic)]
    public async Task Report_AtLevelMinimumVerbosity_Writes(MessageLevel level, Verbosity verbosity)
    {
        using var reporter = new StringWriterReporter(verbosity);
        reporter.Report(level, "something happened");
        await Assert.That(reporter.ErrorText).EndsWith($"something happened{Environment.NewLine}");
        await Assert.That(reporter.OutputText).IsEmpty();
    }

    [Test]
    [Arguments(MessageLevel.Error, "\e[91m", "error")]
    [Arguments(MessageLevel.Warning, "\e[93m", "warning")]
    public async Task Report_ColoredLevelWithColorOn_WrapsOnlyLabelInEscapes(
        MessageLevel level,
        string escape,
        string label)
    {
        using var reporter = new StringWriterReporter(Verbosity.Normal, useColor: true);
        reporter.Report(level, "something happened");
        await Assert.That(reporter.ErrorText).IsEqualTo($"{escape}{label}:\e[0m something happened{Environment.NewLine}");
        await Assert.That(reporter.OutputText).IsEmpty();
    }

    [Test]
    public async Task Report_UncoloredLevelWithColorOn_WritesNoEscapes()
    {
        using var reporter = new StringWriterReporter(Verbosity.Normal, useColor: true);
        reporter.Info("something happened");
        await Assert.That(reporter.ErrorText).IsEqualTo($"info: something happened{Environment.NewLine}");
        await Assert.That(reporter.OutputText).IsEmpty();
    }

    [Test]
    public async Task Report_UnknownLevel_Throws()
    {
        using var reporter = new StringWriterReporter(Verbosity.Diagnostic);

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        await Assert.That(() => reporter.Report((MessageLevel)(-1), "something happened")).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task BeginActivity_AtNormalVerbosity_WritesHeaderToErrorWriter()
    {
        using var reporter = new StringWriterReporter(Verbosity.Normal);
        _ = reporter.BeginActivity("Doing stuff");
        await Assert.That(reporter.ErrorText).IsEqualTo($"[1] Doing stuff: starting...{Environment.NewLine}");
        await Assert.That(reporter.OutputText).IsEmpty();
    }

    [Test]
    public async Task BeginActivity_BelowNormalVerbosity_WritesNothing()
    {
        using var reporter = new StringWriterReporter(Verbosity.Minimal);
        using (var scope = reporter.BeginActivity("Doing stuff"))
        {
            scope.Complete();
        }

        await Assert.That(reporter.ErrorText).IsEmpty();
        await Assert.That(reporter.OutputText).IsEmpty();
    }

    [Test]
    public async Task BeginActivity_Nested_ReflectsDepthInHeaders()
    {
        using var reporter = new StringWriterReporter(Verbosity.Normal);
        using (reporter.BeginActivity("Outer"))
        using (reporter.BeginActivity("Inner"))
        {
        }

        await Assert.That(reporter.ErrorText).Contains("[1] Outer: starting...");
        await Assert.That(reporter.ErrorText).Contains("[2] Inner: starting...");
        await Assert.That(reporter.OutputText).IsEmpty();
    }

    [Test]
    public async Task ActivityScope_Completed_WritesOutcomeToErrorWriter()
    {
        using var reporter = new StringWriterReporter(Verbosity.Normal);
        using (var scope = reporter.BeginActivity("Doing stuff"))
        {
            scope.Complete("all good");
        }

        await Assert.That(reporter.ErrorText).Contains("[1] Doing stuff: done (");
        await Assert.That(reporter.ErrorText).Contains(" - all good");
        await Assert.That(reporter.OutputText).IsEmpty();
    }

    [Test]
    public async Task ActivityScope_NotCompleted_WritesNoOutcome()
    {
        using var reporter = new StringWriterReporter(Verbosity.Normal);
        using (reporter.BeginActivity("Doing stuff"))
        {
        }

        await Assert.That(reporter.ErrorText).IsEqualTo($"[1] Doing stuff: starting...{Environment.NewLine}");
        await Assert.That(reporter.OutputText).IsEmpty();
    }

    [Test]
    public async Task ActivityScope_DisposedTwice_WritesOutcomeOnlyOnce()
    {
        using var reporter = new StringWriterReporter(Verbosity.Normal);
        var scope = reporter.BeginActivity("Doing stuff");
        scope.Complete();
        scope.Dispose();
        scope.Dispose();

        var lines = reporter.ErrorText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        var outcomeLines = lines.Count(static line => line.Contains("done (", StringComparison.Ordinal));
        await Assert.That(outcomeLines).IsEqualTo(1);
        await Assert.That(reporter.OutputText).IsEmpty();
    }

    [Test]
    public async Task ActivityScope_DisposedOutOfOrder_DoesNotPopInnerScope()
    {
        using var reporter = new StringWriterReporter(Verbosity.Normal);
        var outer = reporter.BeginActivity("Outer");
        var inner = reporter.BeginActivity("Inner");
        outer.Complete();
        outer.Dispose();
        inner.Dispose();

        // Disposing Outer while Inner was still open must not pop Inner; Outer itself is never popped, so
        // the next sibling still nests under it.
        using (reporter.BeginActivity("Next"))
        {
        }

        await Assert.That(reporter.ErrorText).Contains("[1] Outer: done (");
        await Assert.That(reporter.ErrorText).Contains("[2] Next: starting...");
        await Assert.That(reporter.OutputText).IsEmpty();
    }

    [Test]
    public async Task ChildOutput_WritesToOutputWriter()
    {
        using var reporter = new StringWriterReporter(Verbosity.Normal);
        reporter.ChildOutput("child stdout line", null);
        await Assert.That(reporter.OutputText).IsEqualTo($"child stdout line{Environment.NewLine}");
        await Assert.That(reporter.ErrorText).IsEmpty();
    }

    [Test]
    public async Task ChildOutput_BelowMinimumVerbosity_WritesNothing()
    {
        using var reporter = new StringWriterReporter(Verbosity.Minimal);
        reporter.ChildOutput("child stdout line", Verbosity.Normal);
        await Assert.That(reporter.OutputText).IsEmpty();
        await Assert.That(reporter.ErrorText).IsEmpty();
    }

    [Test]
    public async Task ChildOutput_AtMinimumVerbosity_Writes()
    {
        using var reporter = new StringWriterReporter(Verbosity.Normal);
        reporter.ChildOutput("child stdout line", Verbosity.Normal);
        await Assert.That(reporter.OutputText).IsEqualTo($"child stdout line{Environment.NewLine}");
        await Assert.That(reporter.ErrorText).IsEmpty();
    }

    [Test]
    public async Task ChildOutput_NoMinimumVerbosity_IgnoresVerbosity()
    {
        using var reporter = new StringWriterReporter(Verbosity.Quiet);
        reporter.ChildOutput("child stdout line", null);
        await Assert.That(reporter.OutputText).IsEqualTo($"child stdout line{Environment.NewLine}");
        await Assert.That(reporter.ErrorText).IsEmpty();
    }

    [Test]
    public async Task ChildError_WritesToErrorWriter()
    {
        using var reporter = new StringWriterReporter(Verbosity.Normal);
        reporter.ChildError("child stderr line", null);
        await Assert.That(reporter.ErrorText).IsEqualTo($"child stderr line{Environment.NewLine}");
        await Assert.That(reporter.OutputText).IsEmpty();
    }

    [Test]
    public async Task ChildError_BelowMinimumVerbosity_WritesNothing()
    {
        using var reporter = new StringWriterReporter(Verbosity.Minimal);
        reporter.ChildError("child stderr line", Verbosity.Normal);
        await Assert.That(reporter.OutputText).IsEmpty();
        await Assert.That(reporter.ErrorText).IsEmpty();
    }

    [Test]
    public async Task ChildError_AtMinimumVerbosity_Writes()
    {
        using var reporter = new StringWriterReporter(Verbosity.Normal);
        reporter.ChildError("child stderr line", Verbosity.Normal);
        await Assert.That(reporter.ErrorText).IsEqualTo($"child stderr line{Environment.NewLine}");
        await Assert.That(reporter.OutputText).IsEmpty();
    }

    [Test]
    public async Task ChildError_NoMinimumVerbosity_IgnoresVerbosity()
    {
        using var reporter = new StringWriterReporter(Verbosity.Quiet);
        reporter.ChildError("child stderr line", null);
        await Assert.That(reporter.ErrorText).IsEqualTo($"child stderr line{Environment.NewLine}");
        await Assert.That(reporter.OutputText).IsEmpty();
    }
}
