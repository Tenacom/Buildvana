// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Buildvana.Core;
using Buildvana.Core.Process;

internal sealed class ProcessRunnerTests
{
    [Test]
    public async Task RunWithInheritedStdio_ReturnsChildExitCode()
    {
        var runner = new ProcessRunner();
        var (executable, args) = ShellCommand("exit 3", "exit 3");

        var exitCode = await runner.RunWithInheritedStdioAsync(executable, args).ConfigureAwait(false);

        await Assert.That(exitCode).IsEqualTo(3);
    }

    [Test]
    public async Task RunWithInheritedStdio_AppliesEnvironmentOverrides()
    {
        var runner = new ProcessRunner();
        var (executable, args) = ShellCommand("exit %BV_TEST_EXIT_CODE%", "exit $BV_TEST_EXIT_CODE");

        var exitCode = await runner.RunWithInheritedStdioAsync(
            executable,
            args,
            environment: new Dictionary<string, string?> { ["BV_TEST_EXIT_CODE"] = "5" }).ConfigureAwait(false);

        await Assert.That(exitCode).IsEqualTo(5);
    }

    // Mutates the process-global environment; must not run alongside other tests (see .claude/rules/testing.md).
    [Test]
    [NotInParallel]
    public async Task RunWithInheritedStdio_RemovesNullEnvironmentEntries()
    {
        // The variable exists in the parent's environment; a null override must remove it from the child's.
        Environment.SetEnvironmentVariable("BV_TEST_REMOVED_VAR", "1");
        try
        {
            var runner = new ProcessRunner();
            var (executable, args) = ShellCommand(
                "if defined BV_TEST_REMOVED_VAR (exit 1) else (exit 9)",
                "if [ -z \"${BV_TEST_REMOVED_VAR+x}\" ]; then exit 9; else exit 1; fi");

            var exitCode = await runner.RunWithInheritedStdioAsync(
                executable,
                args,
                environment: new Dictionary<string, string?> { ["BV_TEST_REMOVED_VAR"] = null }).ConfigureAwait(false);

            await Assert.That(exitCode).IsEqualTo(9);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BV_TEST_REMOVED_VAR", null);
        }
    }

    // Twin of the test above for the CliWrap-backed path: there the "null removes the variable" contract is
    // CliWrap's to honor, not this code's, and this test is what locks it in when CliWrap is updated.
    // Mutates the process-global environment; must not run alongside other tests (see .claude/rules/testing.md).
    [Test]
    [NotInParallel]
    public async Task Run_RemovesNullEnvironmentEntries()
    {
        // The variable exists in the parent's environment; a null override must remove it from the child's.
        Environment.SetEnvironmentVariable("BV_TEST_REMOVED_VAR", "1");
        try
        {
            var runner = new ProcessRunner();
            var (executable, args) = ShellCommand(
                "if defined BV_TEST_REMOVED_VAR (exit 1) else (exit 9)",
                "if [ -z \"${BV_TEST_REMOVED_VAR+x}\" ]; then exit 9; else exit 1; fi");

            var result = await runner.RunAsync(
                executable,
                args,
                environment: new Dictionary<string, string?> { ["BV_TEST_REMOVED_VAR"] = null },
                throwOnNonZero: false).ConfigureAwait(false);

            await Assert.That(result.ExitCode).IsEqualTo(9);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BV_TEST_REMOVED_VAR", null);
        }
    }

    [Test]
    public async Task RunWithInheritedStdio_RunsInGivenWorkingDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("bv-test-");
        try
        {
            // The child proves its working directory by probing for a marker file that exists only there.
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "marker.file"), string.Empty).ConfigureAwait(false);
            var runner = new ProcessRunner();
            var (executable, args) = ShellCommand(
                "if exist marker.file (exit 7) else (exit 1)",
                "if [ -e marker.file ]; then exit 7; else exit 1; fi");

            var exitCode = await runner
                .RunWithInheritedStdioAsync(executable, args, workingDirectory: directory.FullName)
                .ConfigureAwait(false);

            await Assert.That(exitCode).IsEqualTo(7);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    // A failing child is a failing child, whatever number it chose to say so with: the exception states that
    // an external program failed, and the child's own code stays in the message.
    [Test]
    public async Task Run_WhenTheChildFails_StatesThatAnExternalProgramFailed()
    {
        var runner = new ProcessRunner();
        var (executable, args) = ShellCommand("exit 9", "exit 9");

        var exception = await Assert.That(async () => await runner.RunAsync(executable, args).ConfigureAwait(false))
            .Throws<BuildFailedException>();

        await Assert.That(exception!.ExitCode).IsEqualTo(ExitCodes.ExternalProgramFailed);
        await Assert.That(exception.Message).Contains("exited with code 9");
    }

    [Test]
    public async Task RunWithInheritedStdio_WithMissingExecutable_Fails()
    {
        var runner = new ProcessRunner();

        var exception = await Assert.That(() => runner.RunWithInheritedStdioAsync("bv-test-no-such-executable", []))
            .Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains("bv-test-no-such-executable");
    }

    [Test]
    public async Task RunWithInheritedStdio_WhenCancelled_KillsChildAndThrows()
    {
        var runner = new ProcessRunner();
        var (executable, args) = ShellCommand("ping -n 30 127.0.0.1 >nul", "sleep 30");
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var token = cts.Token;
        var stopwatch = Stopwatch.StartNew();

        await Assert.That(() => runner.RunWithInheritedStdioAsync(executable, args, cancellationToken: token))
            .Throws<OperationCanceledException>();

        stopwatch.Stop();
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(15));
    }

    private static (string Executable, string[] Args) ShellCommand(string windows, string posix)
        => OperatingSystem.IsWindows()
            ? ("cmd.exe", ["/d", "/c", windows])
            : ("/bin/sh", ["-c", posix]);
}
