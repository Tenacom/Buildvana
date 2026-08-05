// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Tool.CommandLine;
using Buildvana.Tool.Infrastructure.Execution;

internal sealed class CommandArgumentValidatorTests
{
    [Test]
    public async Task ForwardingCommand_AcceptsTokensAfterSeparator()
    {
        var command = CommandRegistry.Find("build")!;
        var parsed = CliArgSplitter.Split(["build", "--", "-p:Foo=Bar"]);
        CommandArgumentValidator.Validate(command, parsed, parsed.Positionals);
        await Assert.That(parsed.Forwarded.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ForwardingCommand_RejectsOptionsBeforeSeparator()
    {
        var command = CommandRegistry.Find("build")!;
        var parsed = CliArgSplitter.Split(["build", "-p:Foo"]);
        await Assert.That(() => CommandArgumentValidator.Validate(command, parsed, parsed.Positionals)).Throws<BuildFailedException>();
    }

    [Test]
    public async Task ForwardingCommand_RejectsPositionals()
    {
        var command = CommandRegistry.Find("build")!;
        var parsed = CliArgSplitter.Split(["build", "extra"]);
        await Assert.That(() => CommandArgumentValidator.Validate(command, parsed, parsed.Positionals)).Throws<BuildFailedException>();
    }

    [Test]
    public async Task NonForwardingCommand_RejectsTokensAfterSeparator()
    {
        var command = CommandRegistry.Find("release")!;
        var parsed = CliArgSplitter.Split(["release", "--", "x"]);
        await Assert.That(() => CommandArgumentValidator.Validate(command, parsed, parsed.Positionals)).Throws<BuildFailedException>();
    }

    [Test]
    public async Task NonForwardingCommand_AllowsItsOwnOptionTokens()
    {
        var command = CommandRegistry.Find("release")!;
        var parsed = CliArgSplitter.Split(["release", "-c", "Debug"]);
        CommandArgumentValidator.Validate(command, parsed, parsed.Positionals);
        await Assert.That(parsed.OptionTokens.Count).IsEqualTo(2);
    }

    [Test]
    public async Task SettingsLessCommand_RejectsUnknownOption()
    {
        var command = CommandRegistry.Find("clean")!;
        var parsed = CliArgSplitter.Split(["clean", "--bogus"]);
        await Assert.That(() => CommandArgumentValidator.Validate(command, parsed, parsed.Positionals)).Throws<BuildFailedException>();
    }

    [Test]
    public async Task SettingsLessCommand_RejectsUnknownOption_ViaAlias()
    {
        var command = CommandRegistry.Find("version")!;
        var parsed = CliArgSplitter.Split(["version", "--bogus"]);
        await Assert.That(() => CommandArgumentValidator.Validate(command, parsed, parsed.Positionals)).Throws<BuildFailedException>();
    }

    [Test]
    public async Task SettingsCarryingCommand_LeavesOptionTokensToSettings()
    {
        var command = CommandRegistry.Find("release")!;
        var parsed = CliArgSplitter.Split(["release", "--bogus"]);
        CommandArgumentValidator.Validate(command, parsed, parsed.Positionals);
        await Assert.That(parsed.OptionTokens.Count).IsEqualTo(1);
    }

    [Test]
    public async Task NonForwardingCommand_RejectsPositionals_WhenNoArgumentsDeclared()
    {
        var command = CommandRegistry.Find("release")!;
        var parsed = CliArgSplitter.Split(["release", "extra"]);
        await Assert.That(() => CommandArgumentValidator.Validate(command, parsed, parsed.Positionals)).Throws<BuildFailedException>();
    }

    [Test]
    public async Task ArgumentCommand_AcceptsDeclaredPositional()
    {
        var command = CommandRegistry.Find("version advance")!;
        var parsed = CliArgSplitter.Split(["version", "advance", "minor"]);
        CommandArgumentValidator.Validate(command, parsed, ["minor"]);
        await Assert.That(parsed.OptionTokens.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ArgumentCommand_AcceptsOmittedOptionalArgument()
    {
        var command = CommandRegistry.Find("version advance")!;
        var parsed = CliArgSplitter.Split(["version", "advance"]);
        CommandArgumentValidator.Validate(command, parsed, []);
        await Assert.That(parsed.OptionTokens.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ArgumentCommand_RejectsExcessPositionals()
    {
        var command = CommandRegistry.Find("version advance")!;
        var parsed = CliArgSplitter.Split(["version", "advance", "minor", "extra"]);
        await Assert.That(() => CommandArgumentValidator.Validate(command, parsed, ["minor", "extra"])).Throws<BuildFailedException>();
    }

    [Test]
    public async Task ArgumentCommand_RejectsMissingRequiredArgument()
    {
        var command = new CommandRegistration([["fake"]], typeof(FakeArgumentSettings), false, typeof(FakeArgumentSettings));
        var parsed = CliArgSplitter.Split(["fake"]);
        await Assert.That(() => CommandArgumentValidator.Validate(command, parsed, [])).Throws<BuildFailedException>();
    }
}
