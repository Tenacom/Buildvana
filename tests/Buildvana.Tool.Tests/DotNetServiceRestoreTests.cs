// Copyright (C) Tenacom and contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Testing;
using Buildvana.Runtime;
using Buildvana.Tool.Services;
using Buildvana.Tool.Services.Platforms;
using Buildvana.Tool.Services.Solution;
using Buildvana.Tool.Services.Versioning;

internal sealed class DotNetServiceRestoreTests
{
    private const string TreatWarningsAsErrorsOff = "-p:TreatWarningsAsErrors=false";
    private const string WarningsAsErrorsEmpty = "-p:WarningsAsErrors=";

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task RestoreAsync_DisablingWarningsAsErrors_ClearsBothProperties(bool suppressTransitiveOverrides)
    {
        var args = await RestoreAsync(suppressTransitiveOverrides, disableWarningsAsErrors: true).ConfigureAwait(false);

        await Assert.That(args).Contains(TreatWarningsAsErrorsOff);
        await Assert.That(args).Contains(WarningsAsErrorsEmpty);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task RestoreAsync_NotDisablingWarningsAsErrors_LeavesBothPropertiesAlone(bool suppressTransitiveOverrides)
    {
        var args = await RestoreAsync(suppressTransitiveOverrides, disableWarningsAsErrors: false).ConfigureAwait(false);

        await Assert.That(args).DoesNotContain(TreatWarningsAsErrorsOff);
        await Assert.That(args).DoesNotContain(WarningsAsErrorsEmpty);
    }

    // MSBuild takes the last value a command line gives a property, so a configured argument that comes after
    // the two properties would turn the promotion back on.
    [Test]
    public async Task RestoreAsync_DisablingWarningsAsErrors_OverridesConfiguredArguments()
    {
        const string configured = "-p:TreatWarningsAsErrors=true";

        var args = await RestoreAsync(suppressTransitiveOverrides: true, disableWarningsAsErrors: true, configured)
            .ConfigureAwait(false);

        await Assert.That(args.IndexOf(TreatWarningsAsErrorsOff)).IsGreaterThan(args.IndexOf(configured));
        await Assert.That(args.IndexOf(WarningsAsErrorsEmpty)).IsGreaterThan(args.IndexOf(configured));
    }

    private static async Task<List<string>> RestoreAsync(
        bool suppressTransitiveOverrides,
        bool disableWarningsAsErrors,
        params string[] configuredArgs)
    {
        using var home = new TempHome();
        home.WriteFile("Test.slnx", "<Solution />\n");
        var solution = new HomeDirectorySolutionContextFactory(home.Provider).Create();
        var runner = new FakeProcessRunner();
        var config = new BuildvanaConfig { DotNet = new() { Restore = new() { Args = configuredArgs } } };
        var service = new DotNetService(
            NullReporter.Instance,
            runner,
            new LocalPlatform(),
            new Lazy<VersionService>(static () => throw new InvalidOperationException("A restore reads no version.")),
            config);

        _ = await service.RestoreAsync(solution, suppressTransitiveOverrides, disableWarningsAsErrors).ConfigureAwait(false);
        return [.. runner.Runs.Single().Args];
    }
}
