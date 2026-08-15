// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Tool.CommandLine;

internal sealed class CommandLineOverridesParserTests
{
    [Test]
    public async Task Parse_NoTokens_YieldsNoOverrides()
    {
        var overrides = CommandLineOverridesParser.Parse(Parameters());

        await Assert.That(overrides.Configuration).IsNull();
        await Assert.That(overrides.CheckPublicApi).IsNull();
        await Assert.That(overrides.Dogfood).IsNull();
        await Assert.That(overrides.ForwardedArgs).IsNull();
    }

    [Test]
    public async Task Parse_ReadsFlags_FromOptionTokens()
    {
        var overrides = CommandLineOverridesParser.Parse(
            Parameters(options: ["-c", "Debug", "--check-public-api", "false", "--dogfood=true"]));

        await Assert.That(overrides.Configuration).IsEqualTo("Debug");
        await Assert.That(overrides.CheckPublicApi).IsFalse();
        await Assert.That(overrides.Dogfood).IsTrue();
    }

    // A configuration stated among the forwarded arguments decides the actual build, so it must drive
    // bv's own resolution too.
    [Test]
    public async Task Parse_ReadsConfiguration_FromForwardedArgs()
    {
        var overrides = CommandLineOverridesParser.Parse(
            Parameters(forwarded: ["--no-incremental", "-c", "Debug"]));

        await Assert.That(overrides.Configuration).IsEqualTo("Debug");
    }

    [Test]
    public async Task Parse_LastForwardedConfigurationWins()
    {
        var overrides = CommandLineOverridesParser.Parse(
            Parameters(forwarded: ["-c", "A", "--configuration=B"]));

        await Assert.That(overrides.Configuration).IsEqualTo("B");
    }

    // The two sources cannot coexist in a real invocation (release rejects `--`, pipeline commands reject
    // `-c` before it), but the precedence is pinned anyway: bv's own surface wins.
    [Test]
    public async Task Parse_OptionConfiguration_WinsOverForwarded()
    {
        var overrides = CommandLineOverridesParser.Parse(
            Parameters(options: ["-c", "FromOptions"], forwarded: ["-c", "FromForwarded"]));

        await Assert.That(overrides.Configuration).IsEqualTo("FromOptions");
    }

    // Reading the configuration must not consume it: the forwarded arguments reach `dotnet` verbatim.
    [Test]
    public async Task Parse_CapturesForwardedArgsVerbatim()
    {
        var overrides = CommandLineOverridesParser.Parse(
            Parameters(forwarded: ["-c", "Debug", "--no-incremental"]));

        await Assert.That(overrides.ForwardedArgs).IsNotNull();
        await Assert.That(string.Join('|', overrides.ForwardedArgs!)).IsEqualTo("-c|Debug|--no-incremental");
    }

    [Test]
    public async Task Parse_Throws_OnInvalidBool()
    {
        await Assert.That(() => CommandLineOverridesParser.Parse(Parameters(options: ["--dogfood", "maybe"])))
            .Throws<BuildFailedException>();
    }

    private static CommandParameters Parameters(string[]? options = null, string[]? forwarded = null)
        => new(options ?? [], [], forwarded ?? []);
}
