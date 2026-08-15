// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Runtime;
using Buildvana.Tool.Services;

internal sealed class DotNetServiceMergeInvocationTests
{
    // The invocation's arguments (dotnet.all + per-command + forwarded) arrive pre-folded from the
    // configuration factory; MergeInvocation only places them between the base and trailing arguments.
    [Test]
    public async Task MergeInvocation_FoldsBaseInvocationAndTrailingArgsInOrder()
    {
        var invocation = new DotNetInvocationConfig { Args = ["--all-arg", "--restore-arg", "--forwarded"] };

        var args = DotNetService.MergeInvocation(["restore", "sln"], invocation, ["-p:CI=true"]);

        await Assert.That(Join(args)).IsEqualTo("restore|sln|--all-arg|--restore-arg|--forwarded|-p:CI=true");
    }

    [Test]
    public async Task MergeInvocation_EmptyInvocation_LeavesBaseAndTrailingArgsOnly()
    {
        var args = DotNetService.MergeInvocation(
            ["msbuild", "proj", "-getProperty:IsTestingPlatformApplication"],
            new DotNetInvocationConfig(),
            ["-p:CI=false"]);

        await Assert.That(Join(args)).IsEqualTo("msbuild|proj|-getProperty:IsTestingPlatformApplication|-p:CI=false");
    }

    private static string Join(IReadOnlyList<string> tokens) => string.Join('|', tokens);
}
