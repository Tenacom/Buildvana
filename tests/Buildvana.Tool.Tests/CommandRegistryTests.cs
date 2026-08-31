// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Tool.Infrastructure.Execution;

internal sealed class CommandRegistryTests
{
    [Test]
    public async Task Find_IsCaseInsensitive()
    {
        await Assert.That(CommandRegistry.Find("BUILD")?.Name).IsEqualTo("build");
    }

    [Test]
    public async Task Find_ReturnsNull_ForUnknownCommand()
    {
        await Assert.That(CommandRegistry.Find("frobnicate")).IsNull();
    }

    [Test]
    public async Task Find_ResolvesMultiSegmentPaths()
    {
        await Assert.That(CommandRegistry.Find("version advance")?.Name).IsEqualTo("version advance");
    }

    [Test]
    public async Task Find_ResolvesAliases()
    {
        await Assert.That(CommandRegistry.Find("version")?.Name).IsEqualTo("version show");
    }

    [Test]
    public async Task Release_CarriesItsSettingsType()
    {
        await Assert.That(CommandRegistry.Find("release")?.SettingsType).IsNotNull();
    }

    [Test]
    public async Task PipelineCommands_AppearInExecutionOrderBeforeOthers()
    {
        var names = string.Join(",", CommandRegistry.Commands.Select(c => c.Name));
        await Assert.That(names).IsEqualTo(
            "clean,restore,build,test,pack,dependencies show,dependencies update,release,self-update,version advance,version show");
    }

    [Test]
    public async Task TopLevelNodes_ListEachGroupOnce()
    {
        var names = string.Join(",", CommandRegistry.TopLevelNodes.Select(n => n.Name));
        await Assert.That(names).IsEqualTo("clean,restore,build,test,pack,dependencies,deps,release,self-update,version");
    }

    [Test]
    [Arguments("clean", false)]
    [Arguments("restore", true)]
    [Arguments("build", true)]
    [Arguments("test", true)]
    [Arguments("pack", true)]
    [Arguments("dependencies show", true)]
    [Arguments("dependencies update", true)]
    [Arguments("release", true)]
    [Arguments("self-update", false)]
    [Arguments("version show", false)]
    [Arguments("version advance", false)]
    public async Task Commands_DeclareExpectedSdkUsage(string path, bool usesSdk)
    {
        await Assert.That(CommandRegistry.Find(path)?.UsesSdk).IsEqualTo(usesSdk);
    }

    [Test]
    public async Task Resolve_WalksDownToSubcommand()
    {
        var (node, remaining) = CommandRegistry.Resolve("version", ["advance", "minor"]);
        await Assert.That(node.FullName).IsEqualTo("version advance");
        await Assert.That(remaining.Count).IsEqualTo(1);
        await Assert.That(remaining[0]).IsEqualTo("minor");
    }

    [Test]
    public async Task Resolve_BareGroupAlias_LandsOnAliasedCommand()
    {
        var (node, remaining) = CommandRegistry.Resolve("version", []);
        await Assert.That(node.Command?.Name).IsEqualTo("version show");
        await Assert.That(remaining.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Resolve_Throws_OnUnknownCommand()
    {
        await Assert.That(() => CommandRegistry.Resolve("frobnicate", [])).Throws<BuildFailedException>();
    }

    [Test]
    public async Task Resolve_Throws_OnUnknownSubcommand()
    {
        await Assert.That(() => CommandRegistry.Resolve("version", ["frobnicate"])).Throws<BuildFailedException>();
    }

    [Test]
    public async Task BuildTree_Throws_OnDuplicateCommandPath()
    {
        var first = new CommandRegistration([["dup"]], typeof(object), false, null);
        var second = new CommandRegistration([["dup"]], typeof(string), false, null);
        await Assert.That(() => CommandRegistry.BuildTree([first, second])).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task BuildTree_Throws_WhenAliasDuplicatesAnotherCommandPath()
    {
        var first = new CommandRegistration([["dup", "sub"], ["dup"]], typeof(object), false, null);
        var second = new CommandRegistration([["dup"]], typeof(string), false, null);
        await Assert.That(() => CommandRegistry.BuildTree([first, second])).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task BuildTree_MarksAGroupAliasAsAnAliasOfTheGroupItNames()
    {
        var command = new CommandRegistration([["alpha", "show"], ["al", "show"]], typeof(object), false, null);
        var topLevelNodes = CommandRegistry.BuildTree([command]);
        var alias = topLevelNodes.Single(n => n.Name == "al");
        var canonical = topLevelNodes.Single(n => n.Name == "alpha");
        await Assert.That(alias.IsAlias).IsTrue();
        await Assert.That(alias.CanonicalNode).IsSameReferenceAs(canonical);
        await Assert.That(canonical.Aliases.Single()).IsSameReferenceAs(alias);
        await Assert.That(canonical.DisplayName).IsEqualTo("alpha, al");
    }

    [Test]
    public async Task BuildTree_LinksEveryDepthOfAnAliasPath()
    {
        var command = new CommandRegistration([["alpha", "show"], ["al", "show"]], typeof(object), false, null);
        var topLevelNodes = CommandRegistry.BuildTree([command]);
        var alias = topLevelNodes.Single(n => n.Name == "al").FindChild("show")!;
        await Assert.That(alias.CanonicalNode?.FullName).IsEqualTo("alpha show");
    }

    [Test]
    public async Task BuildTree_LeavesAGroupAliasedOntoItsOwnSubcommandCanonical()
    {
        // The shape of `version show | version`: the group node is where two canonical paths pass, so it
        // names itself and is listed as the command it is.
        var show = new CommandRegistration([["version", "show"], ["version"]], typeof(object), false, null);
        var advance = new CommandRegistration([["version", "advance"]], typeof(string), false, null);
        var topLevelNodes = CommandRegistry.BuildTree([show, advance]);
        var group = topLevelNodes.Single();
        await Assert.That(group.IsAlias).IsFalse();
        await Assert.That(group.DisplayName).IsEqualTo("version");
    }

    [Test]
    public async Task BuildTree_Throws_WhenOneAliasNamesTwoCanonicalPaths()
    {
        var first = new CommandRegistration([["alpha", "one"], ["x", "one"]], typeof(object), false, null);
        var second = new CommandRegistration([["beta", "two"], ["x", "two"]], typeof(string), false, null);
        await Assert.That(() => CommandRegistry.BuildTree([first, second])).Throws<InvalidOperationException>();
    }

    // `deps` is the one top-level alias the command tree has: help lists it beside the name it aliases,
    // never as a command of its own.
    [Test]
    public async Task TopLevelNodes_NameTheAliasBesideWhatItAliases()
    {
        var canonical = CommandRegistry.TopLevelNodes.Single(static n => n.Name == "dependencies");
        var alias = CommandRegistry.TopLevelNodes.Single(static n => n.Name == "deps");
        await Assert.That(canonical.IsAlias).IsFalse();
        await Assert.That(alias.CanonicalNode).IsSameReferenceAs(canonical);
        await Assert.That(canonical.DisplayName).IsEqualTo("dependencies, deps");
    }

    [Test]
    public async Task ValidateArgumentOrder_Accepts_RequiredArgumentsFirst()
    {
        var command = new CommandRegistration([["fake"]], typeof(object), false, typeof(FakeOrderedArgumentSettings));
        await Assert.That(() => CommandRegistry.ValidateArgumentOrder([command])).ThrowsNothing();
    }

    [Test]
    public async Task ValidateArgumentOrder_Throws_WhenRequiredArgumentFollowsOptional()
    {
        var command = new CommandRegistration([["fake"]], typeof(object), false, typeof(FakeMisorderedArgumentSettings));
        await Assert.That(() => CommandRegistry.ValidateArgumentOrder([command])).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ValidateArgumentOrder_Accepts_VariadicArgumentLast()
    {
        var command = new CommandRegistration([["fake"]], typeof(object), false, typeof(FakeVariadicArgumentSettings));
        await Assert.That(() => CommandRegistry.ValidateArgumentOrder([command])).ThrowsNothing();
    }

    [Test]
    public async Task ValidateArgumentOrder_Throws_WhenArgumentFollowsVariadic()
    {
        var command = new CommandRegistration([["fake"]], typeof(object), false, typeof(FakeArgumentAfterVariadicSettings));
        await Assert.That(() => CommandRegistry.ValidateArgumentOrder([command])).Throws<InvalidOperationException>();
    }
}
