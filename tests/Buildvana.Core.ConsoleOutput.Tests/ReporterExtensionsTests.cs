// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Testing;

internal sealed class ReporterExtensionsTests
{
    private static readonly CompositeFormat Format = CompositeFormat.Parse("{0} in {1}");

    [Test]
    public async Task MessageShortcuts_ReportAtTheirOwnLevel()
    {
        using var reporter = new StringWriterReporter(Verbosity.Diagnostic);
        reporter.Error("boom");
        reporter.Warning("careful");
        reporter.Notice("noted");
        reporter.Info("working");
        reporter.Detail("details");
        reporter.Trace("trace");
        string[] expected =
        [
            "error: boom",
            "warning: careful",
            "notice: noted",
            "info: working",
            "detail: details",
            "trace: trace",
        ];
        await Assert.That(reporter.ErrorText).IsEqualTo(string.Join(Environment.NewLine, expected) + Environment.NewLine);
        await Assert.That(reporter.OutputText).IsEmpty();
    }

    [Test]
    public async Task FormattingShortcuts_FormatArgumentsAndReportAtTheirOwnLevel()
    {
        using var reporter = new StringWriterReporter(Verbosity.Diagnostic);
        reporter.Error(Format, "boom", 1);
        reporter.Warning(Format, "careful", 2);
        reporter.Notice(Format, "noted", 3);
        reporter.Info(Format, "working", 4);
        reporter.Detail(Format, "details", 5);
        reporter.Trace(Format, "trace", 6);
        string[] expected =
        [
            "error: boom in 1",
            "warning: careful in 2",
            "notice: noted in 3",
            "info: working in 4",
            "detail: details in 5",
            "trace: trace in 6",
        ];
        await Assert.That(reporter.ErrorText).IsEqualTo(string.Join(Environment.NewLine, expected) + Environment.NewLine);
        await Assert.That(reporter.OutputText).IsEmpty();
    }

    [Test]
    public async Task Report_DisabledLevel_WritesNothing()
    {
        using var reporter = new StringWriterReporter(Verbosity.Minimal);
        reporter.Report(MessageLevel.Info, Format, "working", 1);
        await Assert.That(reporter.ErrorText).IsEmpty();
        await Assert.That(reporter.OutputText).IsEmpty();
    }

    [Test]
    public async Task Report_NullFormat_Throws()
    {
        using var reporter = new StringWriterReporter(Verbosity.Diagnostic);

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        await Assert.That(() => reporter.Report(MessageLevel.Info, (CompositeFormat)null!)).Throws<ArgumentNullException>();
    }
}
