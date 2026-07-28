// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.ConsoleOutput;
using Microsoft.Build.Framework;

internal sealed class TaskLoggingHelperReporterTests
{
    [Test]
    public async Task Report_Error_LogsBuildError()
    {
        var (reporter, engine) = CreateReporter();
        reporter.Report(MessageLevel.Error, "boom");
        await Assert.That(engine.Errors.Count).IsEqualTo(1);
        await Assert.That(engine.Errors[0].Message).IsEqualTo("boom");
        await Assert.That(engine.Warnings.Count).IsEqualTo(0);
        await Assert.That(engine.Messages.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Report_Warning_LogsBuildWarning()
    {
        var (reporter, engine) = CreateReporter();
        reporter.Report(MessageLevel.Warning, "uh-oh");
        await Assert.That(engine.Warnings.Count).IsEqualTo(1);
        await Assert.That(engine.Warnings[0].Message).IsEqualTo("uh-oh");
        await Assert.That(engine.Errors.Count).IsEqualTo(0);
        await Assert.That(engine.Messages.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments(MessageLevel.Info, MessageImportance.High)]
    [Arguments(MessageLevel.Detail, MessageImportance.Normal)]
    [Arguments(MessageLevel.Trace, MessageImportance.Low)]
    public async Task Report_MessageLevel_LogsMessageWithExpectedImportance(
        MessageLevel level,
        MessageImportance expectedImportance)
    {
        var (reporter, engine) = CreateReporter();
        reporter.Report(level, "hello");
        await Assert.That(engine.Messages.Count).IsEqualTo(1);
        await Assert.That(engine.Messages[0].Message).IsEqualTo("hello");
        await Assert.That(engine.Messages[0].Importance).IsEqualTo(expectedImportance);
        await Assert.That(engine.Errors.Count).IsEqualTo(0);
        await Assert.That(engine.Warnings.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Report_UnknownLevel_Throws()
    {
        var (reporter, _) = CreateReporter();
        await Assert.That(() => reporter.Report((MessageLevel)42, "x")).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Verbosity_WithoutEngineServices_IsDiagnostic()
    {
        var (reporter, _) = CreateReporter();
        await Assert.That(reporter.Verbosity).IsEqualTo(Verbosity.Diagnostic);
    }

    [Test]
    [Arguments(MessageImportance.Low, Verbosity.Diagnostic)]
    [Arguments(MessageImportance.Normal, Verbosity.Detailed)]
    [Arguments(MessageImportance.High, Verbosity.Normal)]
    [Arguments(null, Verbosity.Minimal)]
    public async Task Verbosity_WithEngineServices_TracksImportanceFiltering(
        MessageImportance? minimumImportance,
        Verbosity expectedVerbosity)
    {
        var (reporter, _) = CreateReporter(minimumImportance);
        await Assert.That(reporter.Verbosity).IsEqualTo(expectedVerbosity);
    }

    [Test]
    public async Task BeginActivity_LogsStartLine()
    {
        var (reporter, engine) = CreateReporter();
        using var scope = reporter.BeginActivity("Frobbing");
        await Assert.That(engine.Messages.Count).IsEqualTo(1);
        await Assert.That(engine.Messages[0].Message).IsEqualTo("[1] Frobbing: starting...");
        await Assert.That(engine.Messages[0].Importance).IsEqualTo(MessageImportance.Normal);
    }

    [Test]
    public async Task ActivityScope_CompleteThenDispose_LogsOutcomeLine()
    {
        var (reporter, engine) = CreateReporter();
        var scope = reporter.BeginActivity("Frobbing");
        scope.Complete("all good");
        scope.Dispose();
        await Assert.That(engine.Messages.Count).IsEqualTo(2);
        await Assert.That(engine.Messages[1].Message).StartsWith("[1] Frobbing: done (");
        await Assert.That(engine.Messages[1].Message).EndsWith(" - all good");
        await Assert.That(engine.Messages[1].Importance).IsEqualTo(MessageImportance.Normal);
    }

    [Test]
    public async Task ActivityScope_DisposeWithoutComplete_LogsNoOutcomeLine()
    {
        var (reporter, engine) = CreateReporter();
        var scope = reporter.BeginActivity("Frobbing");
        scope.Dispose();
        scope.Dispose();
        await Assert.That(engine.Messages.Count).IsEqualTo(1);
    }

    [Test]
    public async Task BeginActivity_Nested_TracksDepth()
    {
        var (reporter, engine) = CreateReporter();
        using var outer = reporter.BeginActivity("Outer");
        var inner = reporter.BeginActivity("Inner");
        inner.Complete();
        inner.Dispose();
        await Assert.That(engine.Messages.Count).IsEqualTo(3);
        await Assert.That(engine.Messages[1].Message).IsEqualTo("[2] Inner: starting...");
        await Assert.That(engine.Messages[2].Message).StartsWith("[2] Inner: done (");
    }

    [Test]
    public async Task ChildOutput_LogsLowImportanceMessage()
    {
        var (reporter, engine) = CreateReporter();
        reporter.ChildOutput("raw stdout line", null);
        await Assert.That(engine.Messages.Count).IsEqualTo(1);
        await Assert.That(engine.Messages[0].Message).IsEqualTo("raw stdout line");
        await Assert.That(engine.Messages[0].Importance).IsEqualTo(MessageImportance.Low);
    }

    [Test]
    public async Task ChildError_LogsLowImportanceMessage_NotABuildError()
    {
        var (reporter, engine) = CreateReporter();
        reporter.ChildError("raw stderr line", null);
        await Assert.That(engine.Errors.Count).IsEqualTo(0);
        await Assert.That(engine.Warnings.Count).IsEqualTo(0);
        await Assert.That(engine.Messages.Count).IsEqualTo(1);
        await Assert.That(engine.Messages[0].Message).IsEqualTo("raw stderr line");
        await Assert.That(engine.Messages[0].Importance).IsEqualTo(MessageImportance.Low);
    }

    [Test]
    public async Task ChildLines_WhenEngineDiscardsLowImportance_AreNotForwarded()
    {
        // Dropped twice over: the reporter's minimumVerbosity gate (verbosity here is Normal) and
        // TaskLoggingHelper's own importance filtering both consult the same EngineServices.
        var (reporter, engine) = CreateReporter(MessageImportance.High);
        reporter.ChildOutput("out", null);
        reporter.ChildOutput("out gated", Verbosity.Diagnostic);
        reporter.ChildError("err", null);
        reporter.ChildError("err gated", Verbosity.Diagnostic);
        await Assert.That(engine.Messages.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ChildLines_WhenEngineLogsLowImportance_AreForwarded()
    {
        var (reporter, engine) = CreateReporter(MessageImportance.Low);
        reporter.ChildOutput("out", Verbosity.Diagnostic);
        reporter.ChildError("err", Verbosity.Diagnostic);
        await Assert.That(engine.Messages.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Reporter_IsCachedPerTask()
    {
        var task = new ReporterProbeTask { BuildEngine = new RecordingBuildEngine() };
        await Assert.That(task.ExposedReporter).IsSameReferenceAs(task.ExposedReporter);
    }

    private static (IReporter Reporter, RecordingBuildEngine Engine) CreateReporter()
    {
        var engine = new RecordingBuildEngine();
        var task = new ReporterProbeTask { BuildEngine = engine };
        return (task.ExposedReporter, engine);
    }

    private static (IReporter Reporter, RecordingBuildEngine Engine) CreateReporter(
        MessageImportance? minimumImportance)
    {
        var engine = new RecordingBuildEngine10
        {
            EngineServices = new StubEngineServices { MinimumImportance = minimumImportance },
        };
        var task = new ReporterProbeTask { BuildEngine = engine };
        return (task.ExposedReporter, engine);
    }
}
