// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Services;

internal sealed class DotNetServiceMergeInvocationTests
{
    [Test]
    public async Task MergeInvocation_FoldsBaseTierCommandLineAndTrailingArgsInOrder()
    {
        var all = Settings(["--all-arg"], ("A", "1"));
        var restore = Settings(["--restore-arg"], ("B", "2"));

        var (args, env) = DotNetService.MergeInvocation(
            ["restore", "sln"],
            [all, restore],
            ["--cli"],
            ["-p:CI=true"]);

        await Assert.That(Join(args)).IsEqualTo("restore|sln|--all-arg|--restore-arg|--cli|-p:CI=true");
        await Assert.That(env).IsNotNull();
        await Assert.That(env!.Count).IsEqualTo(2);
        await Assert.That(env["A"]).IsEqualTo("1");
        await Assert.That(env["B"]).IsEqualTo("2");
    }

    [Test]
    public async Task MergeInvocation_LaterTierOverridesEarlierTierEnvVariable()
    {
        var all = Settings([], ("KEY", "from-all"), ("ONLY_ALL", "kept"));
        var build = Settings([], ("KEY", "from-build"));

        var (_, env) = DotNetService.MergeInvocation([], [all, build], [], []);

        await Assert.That(env).IsNotNull();
        await Assert.That(env!.Count).IsEqualTo(2);
        await Assert.That(env["KEY"]).IsEqualTo("from-build");
        await Assert.That(env["ONLY_ALL"]).IsEqualTo("kept");
    }

    [Test]
    public async Task MergeInvocation_PreservesNullEnvValueAsRemoval()
    {
        var tier = Settings([], ("REMOVE_ME", null));

        var (_, env) = DotNetService.MergeInvocation([], [tier], [], []);

        await Assert.That(env).IsNotNull();
        await Assert.That(env!.ContainsKey("REMOVE_ME")).IsTrue();
        await Assert.That(env["REMOVE_ME"]).IsNull();
    }

    [Test]
    public async Task MergeInvocation_WithoutTiersSkipsConfiguredArgsAndLeavesEnvironmentUnchanged()
    {
        var (args, env) = DotNetService.MergeInvocation(
            ["msbuild", "proj", "-getProperty:IsTestingPlatformApplication"],
            [],
            ["--cli"],
            ["-p:CI=false"]);

        await Assert.That(Join(args)).IsEqualTo("msbuild|proj|-getProperty:IsTestingPlatformApplication|--cli|-p:CI=false");
        await Assert.That(env).IsNull();
    }

    [Test]
    public async Task MergeInvocation_WithTiersButNoEnvVariablesLeavesEnvironmentUnchanged()
    {
        var all = Settings(["--all-arg"]);
        var build = Settings(["--build-arg"]);

        var (args, env) = DotNetService.MergeInvocation(["build"], [all, build], [], []);

        await Assert.That(Join(args)).IsEqualTo("build|--all-arg|--build-arg");
        await Assert.That(env).IsNull();
    }

    private static DotNetInvocationSettings Settings(IReadOnlyList<string> args, params (string Key, string? Value)[] env)
    {
        var dictionary = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, value) in env)
        {
            dictionary[key] = value;
        }

        return new DotNetInvocationSettings(args, dictionary);
    }

    private static string Join(IReadOnlyList<string> tokens) => string.Join('|', tokens);
}
