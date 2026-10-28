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
        var command = await Assert.That(CommandRegistry.Find("build")).IsNotNull();
        var parsed = CliArgSplitter.Split(["build", "--", "-p:Foo=Bar"]);
        var parameters = CommandArgumentValidator.Validate(command, parsed, parsed.Positionals);
        await Assert.That(Join(parameters.Forwarded)).IsEqualTo("-p:Foo=Bar");
    }

    [Test]
    public async Task ForwardingCommand_RejectsOptionsBeforeSeparator()
    {
        var command = await Assert.That(CommandRegistry.Find("build")).IsNotNull();
        var parsed = CliArgSplitter.Split(["build", "-p:Foo"]);
        await Assert.That(() => CommandArgumentValidator.Validate(command, parsed, parsed.Positionals)).Throws<BuildFailedException>();
    }

    [Test]
    public async Task ForwardingCommand_RejectsPositionals()
    {
        var command = await Assert.That(CommandRegistry.Find("build")).IsNotNull();
        var parsed = CliArgSplitter.Split(["build", "extra"]);
        await Assert.That(() => CommandArgumentValidator.Validate(command, parsed, parsed.Positionals)).Throws<BuildFailedException>();
    }

    [Test]
    public async Task NonForwardingCommand_RejectsTokensAfterSeparator()
    {
        var command = await Assert.That(CommandRegistry.Find("release")).IsNotNull();
        var parsed = CliArgSplitter.Split(["release", "--", "x"]);
        await Assert.That(() => CommandArgumentValidator.Validate(command, parsed, parsed.Positionals)).Throws<BuildFailedException>();
    }

    [Test]
    public async Task NonForwardingCommand_AllowsItsOwnOptionTokens()
    {
        var command = await Assert.That(CommandRegistry.Find("release")).IsNotNull();
        var parsed = CliArgSplitter.Split(["release", "-c", "Debug"]);
        var parameters = CommandArgumentValidator.Validate(command, parsed, parsed.Positionals);
        await Assert.That(Join(parameters.Options)).IsEqualTo("-c|Debug");
    }

    [Test]
    public async Task SettingsLessCommand_RejectsUnknownOption()
    {
        var command = await Assert.That(CommandRegistry.Find("clean")).IsNotNull();
        var parsed = CliArgSplitter.Split(["clean", "--bogus"]);
        await Assert.That(() => CommandArgumentValidator.Validate(command, parsed, parsed.Positionals))
            .Throws<BuildFailedException>()
            .WithMessage("Unknown option '--bogus' for command 'clean'.", StringComparison.Ordinal);
    }

    [Test]
    public async Task SettingsLessCommand_RejectsUnknownOption_ViaAlias()
    {
        var command = await Assert.That(CommandRegistry.Find("version")).IsNotNull();
        var parsed = CliArgSplitter.Split(["version", "--bogus"]);
        await Assert.That(() => CommandArgumentValidator.Validate(command, parsed, parsed.Positionals))
            .Throws<BuildFailedException>()
            .WithMessage("Unknown option '--bogus' for command 'version show'.", StringComparison.Ordinal);
    }

    [Test]
    public async Task SettingsLessCommand_ReportsOffendingTokensInCommandLineOrder()
    {
        var command = await Assert.That(CommandRegistry.Find("clean")).IsNotNull();
        var parsed = CliArgSplitter.Split(["clean", "junk", "--bogus"]);
        await Assert.That(() => CommandArgumentValidator.Validate(command, parsed, parsed.Positionals))
            .Throws<BuildFailedException>()
            .WithMessage("Unexpected argument 'junk' for command 'clean'.", StringComparison.Ordinal);
    }

    [Test]
    public async Task SettingsCarryingCommand_RejectsUnknownOption()
    {
        var command = await Assert.That(CommandRegistry.Find("release")).IsNotNull();
        var parsed = CliArgSplitter.Split(["release", "--bogus"]);
        await Assert.That(() => CommandArgumentValidator.Validate(command, parsed, parsed.Positionals))
            .Throws<BuildFailedException>()
            .WithMessage("Unknown option '--bogus' for command 'release'.", StringComparison.Ordinal);
    }

    [Test]
    public async Task SettingsCarryingCommand_ConsumesOptionValue()
    {
        var command = await Assert.That(CommandRegistry.Find("release")).IsNotNull();
        var parsed = CliArgSplitter.Split(["release", "--bump", "minor"]);
        var parameters = CommandArgumentValidator.Validate(command, parsed, parsed.Positionals);
        await Assert.That(Join(parameters.Options)).IsEqualTo("--bump|minor");
        await Assert.That(parameters.Positionals.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SettingsCarryingCommand_AllowsInlineOptionValue()
    {
        var command = await Assert.That(CommandRegistry.Find("release")).IsNotNull();
        var parsed = CliArgSplitter.Split(["release", "--bump=minor"]);
        var parameters = CommandArgumentValidator.Validate(command, parsed, parsed.Positionals);
        await Assert.That(Join(parameters.Options)).IsEqualTo("--bump=minor");
    }

    [Test]
    public async Task SettingsCarryingCommand_AllowsFlagOption()
    {
        var command = await Assert.That(CommandRegistry.Find("version advance")).IsNotNull();
        var parsed = CliArgSplitter.Split(["version", "advance", "--force"]);
        var parameters = CommandArgumentValidator.Validate(command, parsed, []);
        await Assert.That(Join(parameters.Options)).IsEqualTo("--force");
    }

    [Test]
    public async Task SettingsCarryingCommand_RejectsValueOptionWithoutValue()
    {
        var command = await Assert.That(CommandRegistry.Find("release")).IsNotNull();
        var parsed = CliArgSplitter.Split(["release", "--bump"]);
        await Assert.That(() => CommandArgumentValidator.Validate(command, parsed, parsed.Positionals))
            .Throws<BuildFailedException>()
            .WithMessage("Option '--bump' requires a value.", StringComparison.Ordinal);
    }

    [Test]
    public async Task NonForwardingCommand_RejectsPositionals_WhenNoArgumentsDeclared()
    {
        var command = await Assert.That(CommandRegistry.Find("release")).IsNotNull();
        var parsed = CliArgSplitter.Split(["release", "extra"]);
        await Assert.That(() => CommandArgumentValidator.Validate(command, parsed, parsed.Positionals)).Throws<BuildFailedException>();
    }

    [Test]
    public async Task ArgumentCommand_AcceptsDeclaredPositional()
    {
        var command = await Assert.That(CommandRegistry.Find("version advance")).IsNotNull();
        var parsed = CliArgSplitter.Split(["version", "advance", "minor"]);
        var parameters = CommandArgumentValidator.Validate(command, parsed, ["minor"]);
        await Assert.That(Join(parameters.Positionals)).IsEqualTo("minor");
        await Assert.That(parameters.Options.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ArgumentCommand_AcceptsOmittedOptionalArgument()
    {
        var command = await Assert.That(CommandRegistry.Find("version advance")).IsNotNull();
        var parsed = CliArgSplitter.Split(["version", "advance"]);
        var parameters = CommandArgumentValidator.Validate(command, parsed, []);
        await Assert.That(parameters.Positionals.Count).IsEqualTo(0);
        await Assert.That(parameters.Options.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ArgumentCommand_RejectsExcessPositionals()
    {
        var command = await Assert.That(CommandRegistry.Find("version advance")).IsNotNull();
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

    [Test]
    public async Task VariadicArgumentCommand_AcceptsMorePositionalsThanDeclaredArguments()
    {
        var command = new CommandRegistration(
            [["fake"]],
            typeof(FakeVariadicArgumentSettings),
            false,
            typeof(FakeVariadicArgumentSettings));
        var parsed = CliArgSplitter.Split(["fake", "mode", "one", "two", "three"]);
        await Assert.That(() => CommandArgumentValidator.Validate(command, parsed, ["mode", "one", "two", "three"])).ThrowsNothing();
    }

    [Test]
    public async Task VariadicArgumentCommand_AcceptsNoPositionalAtAll()
    {
        var command = new CommandRegistration(
            [["fake"]],
            typeof(FakeVariadicArgumentSettings),
            false,
            typeof(FakeVariadicArgumentSettings));
        var parsed = CliArgSplitter.Split(["fake"]);
        await Assert.That(() => CommandArgumentValidator.Validate(command, parsed, [])).ThrowsNothing();
    }

    [Test]
    public async Task OperandAfterFlag_BindsAsPositional()
    {
        var command = await Assert.That(CommandRegistry.Find("deps update")).IsNotNull();
        var parsed = CliArgSplitter.Split(["deps", "update", "--check", "CommunityToolkit.Diagnostics"]);
        var parameters = CommandArgumentValidator.Validate(command, parsed, []);
        await Assert.That(Join(parameters.Positionals)).IsEqualTo("CommunityToolkit.Diagnostics");
        await Assert.That(Join(parameters.Options)).IsEqualTo("--check");
    }

    [Test]
    public async Task OperandAfterValueOption_BindsAsPositional()
    {
        var command = await Assert.That(CommandRegistry.Find("deps update")).IsNotNull();
        var parsed = CliArgSplitter.Split(["deps", "update", "--to", "1.2.3", "Serilog"]);
        var parameters = CommandArgumentValidator.Validate(command, parsed, []);
        await Assert.That(Join(parameters.Positionals)).IsEqualTo("Serilog");
        await Assert.That(Join(parameters.Options)).IsEqualTo("--to|1.2.3");
    }

    [Test]
    public async Task OperandsOnBothSidesOfAnOption_BindInCommandLineOrder()
    {
        var command = await Assert.That(CommandRegistry.Find("deps update")).IsNotNull();
        var parsed = CliArgSplitter.Split(["deps", "update", "Serilog", "--check", "Louis"]);
        var parameters = CommandArgumentValidator.Validate(command, parsed, ["Serilog"]);
        await Assert.That(Join(parameters.Positionals)).IsEqualTo("Serilog|Louis");
        await Assert.That(Join(parameters.Options)).IsEqualTo("--check");
    }

    [Test]
    public async Task UnknownOptionBeforeAnOperand_IsReportedFirst()
    {
        var command = await Assert.That(CommandRegistry.Find("deps update")).IsNotNull();
        var parsed = CliArgSplitter.Split(["deps", "update", "--bogus", "Serilog"]);
        await Assert.That(() => CommandArgumentValidator.Validate(command, parsed, []))
            .Throws<BuildFailedException>()
            .WithMessage("Unknown option '--bogus' for command 'dependencies update'.", StringComparison.Ordinal);
    }

    [Test]
    public async Task ExcessOperandAfterAnOption_IsRejected()
    {
        var command = await Assert.That(CommandRegistry.Find("version advance")).IsNotNull();
        var parsed = CliArgSplitter.Split(["version", "advance", "--force", "minor", "extra"]);
        await Assert.That(() => CommandArgumentValidator.Validate(command, parsed, []))
            .Throws<BuildFailedException>()
            .WithMessage("Unexpected argument 'extra' for command 'version advance'.", StringComparison.Ordinal);
    }

    [Test]
    public async Task OperandRepeatingAConsumedOptionValue_LeavesTheOptionTokensInOrder()
    {
        var command = new CommandRegistration([["fake"]], typeof(FakeValueOptionSettings), false, typeof(FakeValueOptionSettings));
        var parsed = CliArgSplitter.Split(["fake", "--to", "1.0.0", "--force", "1.0.0"]);
        var parameters = CommandArgumentValidator.Validate(command, parsed, []);
        await Assert.That(Join(parameters.Options)).IsEqualTo("--to|1.0.0|--force");
        await Assert.That(Join(parameters.Positionals)).IsEqualTo("1.0.0");
    }

    private static string Join(IReadOnlyList<string> tokens) => string.Join('|', tokens);
}
