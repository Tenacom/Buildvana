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
        CommandArgumentValidator.Validate(command, parsed);
        await Assert.That(parsed.Forwarded.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ForwardingCommand_RejectsOptionsBeforeSeparator()
    {
        var command = CommandRegistry.Find("build")!;
        var parsed = CliArgSplitter.Split(["build", "-p:Foo"]);
        await Assert.That(() => CommandArgumentValidator.Validate(command, parsed)).Throws<BuildFailedException>();
    }

    [Test]
    public async Task ForwardingCommand_RejectsPositionals()
    {
        var command = CommandRegistry.Find("build")!;
        var parsed = CliArgSplitter.Split(["build", "extra"]);
        await Assert.That(() => CommandArgumentValidator.Validate(command, parsed)).Throws<BuildFailedException>();
    }

    [Test]
    public async Task NonForwardingCommand_RejectsTokensAfterSeparator()
    {
        var command = CommandRegistry.Find("release")!;
        var parsed = CliArgSplitter.Split(["release", "--", "x"]);
        await Assert.That(() => CommandArgumentValidator.Validate(command, parsed)).Throws<BuildFailedException>();
    }

    [Test]
    public async Task NonForwardingCommand_AllowsItsOwnOptionTokens()
    {
        var command = CommandRegistry.Find("release")!;
        var parsed = CliArgSplitter.Split(["release", "-c", "Debug"]);
        CommandArgumentValidator.Validate(command, parsed);
        await Assert.That(parsed.OptionTokens.Count).IsEqualTo(2);
    }
}
