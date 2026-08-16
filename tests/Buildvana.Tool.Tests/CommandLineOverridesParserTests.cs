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

    // Both spellings are bv's own, so both are consumed: nothing is left to forward.
    [Test]
    public async Task Parse_LastForwardedConfigurationWins()
    {
        var overrides = CommandLineOverridesParser.Parse(
            Parameters(forwarded: ["-c", "A", "--configuration=B"]));

        await Assert.That(overrides.Configuration).IsEqualTo("B");
        await Assert.That(overrides.ForwardedArgs).IsNull();
    }

    // The two sources cannot coexist in a real invocation (release rejects `--`, pipeline commands reject
    // `-c` before it), but the precedence is pinned anyway: bv's own surface wins. The forwarded occurrence
    // is consumed regardless, so the stream never carries the option onward.
    [Test]
    public async Task Parse_OptionConfiguration_WinsOverForwarded()
    {
        var overrides = CommandLineOverridesParser.Parse(
            Parameters(options: ["-c", "FromOptions"], forwarded: ["-c", "FromForwarded"]));

        await Assert.That(overrides.Configuration).IsEqualTo("FromOptions");
        await Assert.That(overrides.ForwardedArgs).IsNull();
    }

    // bv owns `-c`/`--configuration` in the forwarded stream: the recognized tokens are promoted and
    // stripped, and only the remainder reaches `dotnet` (`dotnet restore` would reject `-c`).
    [Test]
    public async Task Parse_StripsConfigurationTokens_FromForwardedArgs()
    {
        var overrides = CommandLineOverridesParser.Parse(
            Parameters(forwarded: ["-c", "Debug", "--no-incremental"]));

        await Assert.That(overrides.Configuration).IsEqualTo("Debug");
        await Assert.That(overrides.ForwardedArgs).IsNotNull();
        await Assert.That(string.Join('|', overrides.ForwardedArgs!)).IsEqualTo("--no-incremental");
    }

    [Test]
    public async Task Parse_KeepsOtherForwardedArgsVerbatim()
    {
        var overrides = CommandLineOverridesParser.Parse(
            Parameters(forwarded: ["-m:8", "-v:minimal"]));

        await Assert.That(overrides.Configuration).IsNull();
        await Assert.That(overrides.ForwardedArgs).IsNotNull();
        await Assert.That(string.Join('|', overrides.ForwardedArgs!)).IsEqualTo("-m:8|-v:minimal");
    }

    // The cost of ownership: a trailing `-c` that was meant as another forwarded option's value is read
    // as bv's own option with its value missing.
    [Test]
    public async Task Parse_Throws_OnTrailingForwardedConfiguration()
    {
        await Assert.That(() => CommandLineOverridesParser.Parse(Parameters(forwarded: ["--treenode-filter", "-c"])))
            .Throws<BuildFailedException>();
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
