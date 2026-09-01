// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Tool.Services.Dependencies;
using Buildvana.Tool.Subcommands;

internal sealed class DependenciesUpdateSettingsTests
{
    [Test]
    public async Task Parse_WithNoOption_AppliesEveryConfiguredScope()
    {
        var settings = DependenciesUpdateSettings.Parse([], []);
        await Assert.That(settings.Included).IsEmpty();
        await Assert.That(settings.Excluded).IsEmpty();
        await Assert.That(settings.Check).IsFalse();
        await Assert.That(settings.All).IsFalse();
    }

    [Test]
    public async Task Parse_ReadsTheScopeFlags()
    {
        var settings = DependenciesUpdateSettings.Parse([], ["--tools", "--sdks"]);
        await Assert.That(settings.Included).IsEquivalentTo([DependencyScope.Sdks, DependencyScope.Tools]);
        await Assert.That(settings.Excluded).IsEmpty();
    }

    [Test]
    public async Task Parse_ReadsCheckAndAll()
    {
        var settings = DependenciesUpdateSettings.Parse([], ["--check", "--all"]);
        await Assert.That(settings.Check).IsTrue();
        await Assert.That(settings.All).IsTrue();
    }

    [Test]
    public async Task Parse_WithAllAndNoCheck_IsRefused()
    {
        var exception = await Assert.That(() => DependenciesUpdateSettings.Parse([], ["--all"])).Throws<BuildFailedException>();
        await Assert.That(exception!.ExitCode).IsEqualTo(ExitCodes.Usage);
    }

    [Test]
    public async Task Parse_ReadsThePinsTheArgumentsName()
    {
        var settings = DependenciesUpdateSettings.Parse(["Serilog", "Microsoft.*"], []);
        await Assert.That(settings.Filters).IsEquivalentTo(["Serilog", "Microsoft.*"]);
    }

    [Test]
    public async Task Parse_ReadsTheStatedVersion()
    {
        var settings = DependenciesUpdateSettings.Parse(["Serilog"], ["--to", "3.1.0"]);
        await Assert.That(settings.To?.ToNormalizedString()).IsEqualTo("3.1.0");
    }

    [Test]
    public async Task Parse_WithAVersionThatDoesNotParse_IsRefused()
    {
        var exception = await Assert.That(() => DependenciesUpdateSettings.Parse(["Serilog"], ["--to", "not-a-version"]))
            .Throws<BuildFailedException>();
        await Assert.That(exception!.ExitCode).IsEqualTo(ExitCodes.Usage);
    }

    [Test]
    public async Task Parse_WithToAndCheck_IsRefused()
    {
        var exception = await Assert.That(() => DependenciesUpdateSettings.Parse(["Serilog"], ["--to", "3.1.0", "--check"]))
            .Throws<BuildFailedException>();
        await Assert.That(exception!.ExitCode).IsEqualTo(ExitCodes.Usage);
    }

    [Test]
    public async Task Parse_WithNetSdkAndAnArgument_IsRefused()
    {
        var exception = await Assert.That(() => DependenciesUpdateSettings.Parse(["Serilog"], ["--netsdk"]))
            .Throws<BuildFailedException>();
        await Assert.That(exception!.ExitCode).IsEqualTo(ExitCodes.Usage);
    }

    [Test]
    public async Task Parse_WithToAndAPattern_IsRefused()
    {
        var exception = await Assert.That(() => DependenciesUpdateSettings.Parse(["Serilog.*"], ["--to", "3.1.0"]))
            .Throws<BuildFailedException>();
        await Assert.That(exception!.ExitCode).IsEqualTo(ExitCodes.Usage);
    }

    [Test]
    public async Task Parse_WithToAndSeveralArguments_IsRefused()
    {
        var exception = await Assert.That(() => DependenciesUpdateSettings.Parse(["Serilog", "Newtonsoft.Json"], ["--to", "3.1.0"]))
            .Throws<BuildFailedException>();
        await Assert.That(exception!.ExitCode).IsEqualTo(ExitCodes.Usage);
    }
}
