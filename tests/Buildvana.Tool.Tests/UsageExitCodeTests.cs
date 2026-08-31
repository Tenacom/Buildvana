// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Tool.CommandLine;
using Buildvana.Tool.Infrastructure.Execution;
using Buildvana.Tool.Subcommands;

// bv refuses an invocation whose shape it cannot accept, and exits with ExitCodes.Usage when it does.
// Each test here triggers one of the sites that raise that refusal; the messages themselves are pinned by
// the tests of the components that raise them.
internal sealed class UsageExitCodeTests
{
    [Test]
    [Arguments("bogus")] // unknown command
    [Arguments("version bogus")] // unknown subcommand
    [Arguments("build extra")] // a token before -- on a command that forwards
    [Arguments("release -- x")] // a token after -- on a command that does not forward
    [Arguments("release extra")] // a positional on a command that declares none
    [Arguments("clean --bogus")] // unknown option
    [Arguments("release --bump")] // a value option with nothing after it
    [Arguments("release --bump=")] // a value option with a blank value
    public Task RejectedCommandLine_ExitsWithUsageCode(string commandLine)
        => AssertUsageExitCode(() => ParseCommandLine(commandLine));

    [Test]
    public Task MissingRequiredArgument_ExitsWithUsageCode()
    {
        // No real command declares a required argument, so the validator is fed a settings double, as its own
        // tests feed it one.
        var command = new CommandRegistration([["fake"]], typeof(FakeArgumentSettings), false, typeof(FakeArgumentSettings));
        var parsed = CliArgSplitter.Split(["fake"]);
        return AssertUsageExitCode(() => CommandArgumentValidator.Validate(command, parsed, []));
    }

    [Test]
    public Task UnknownVerbosity_ExitsWithUsageCode()
        => AssertUsageExitCode(() => VerbosityParser.Parse("bogus"));

    [Test]
    public Task InvalidBooleanOptionValue_ExitsWithUsageCode()
        => AssertUsageExitCode(() => new CliOptionReader(["--dogfood", "maybe"]).ReadBoolValue("--dogfood"));

    [Test]
    public Task InvalidBumpValue_ExitsWithUsageCode()
        => AssertUsageExitCode(() => ReleaseSettings.Parse(["--bump", "bogus"]).ResolveBump());

    [Test]
    public Task InvalidToVersion_ExitsWithUsageCode()
        => AssertUsageExitCode(() => SelfUpdateSettings.Parse(["--to", "bogus"]).ResolveTo());

    [Test]
    public Task InvalidChangeArgument_ExitsWithUsageCode()
        => AssertUsageExitCode(() => VersionAdvanceSettings.Parse(["bogus"], []).ResolveChange());

    // The dispatch path of Program, up to the point where the command would run: resolve the command, then
    // validate what was given to it.
    private static void ParseCommandLine(string commandLine)
    {
        var parsed = CliArgSplitter.Split(commandLine.Split(' '));
        var (node, positionals) = CommandRegistry.Resolve(parsed.Subcommand!, parsed.Positionals);
        CommandArgumentValidator.Validate(node.Command!, parsed, positionals);
    }

    private static async Task AssertUsageExitCode(Action action)
    {
        var exception = await Assert.That(action).Throws<BuildFailedException>();
        await Assert.That(exception!.ExitCode).IsEqualTo(ExitCodes.Usage);
    }
}
