// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.Configuration;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Process;
using Buildvana.Core.Testing;
using Buildvana.Tool.Services.Dependencies;
using NuGet.Versioning;

internal sealed class ToolPinUpdaterTests
{
    [Test]
    public async Task UpdateAsync_DelegatesEachToolToTheCli()
    {
        using var home = new TempHome();
        var runner = new FakeProcessRunner();
        await UpdateAsync(home, runner, Moving("ngbv", "0.5.1", "0.6.0"), Moving("bv", "2.1.0", "2.2.0")).ConfigureAwait(false);
        await Assert.That(runner.Runs.Count).IsEqualTo(2);
        await Assert.That(string.Join(" ", runner.Runs[0].Args)).IsEqualTo("tool update ngbv --local --version 0.6.0");
        await Assert.That(string.Join(" ", runner.Runs[1].Args)).IsEqualTo("tool update bv --local --version 2.2.0");
        await Assert.That(runner.Runs[0].WorkingDirectory).IsEqualTo(home.RootPath);
    }

    // The CLI refuses to lower a tool's version unless told that lowering it is the point.
    [Test]
    public async Task UpdateAsync_OfADowngrade_TellsTheCliToAllowIt()
    {
        using var home = new TempHome();
        var runner = new FakeProcessRunner();
        await UpdateAsync(home, runner, Moving("ngbv", "0.6.0", "0.5.1")).ConfigureAwait(false);
        await Assert.That(string.Join(" ", runner.Runs.Single().Args)).EndsWith("--allow-downgrade");
    }

    [Test]
    public async Task UpdateAsync_LeavesAToolThatDoesNotMoveAlone()
    {
        using var home = new TempHome();
        var runner = new FakeProcessRunner();
        var pin = DependencyPin.Create(DependencyScope.Tools, "ngbv", "0.5.1", ".config/dotnet-tools.json");
        var resolution = new PinResolution { Pin = pin, Policy = Policy(), State = PinResolutionState.UpToDate };
        await UpdateAsync(home, runner, resolution).ConfigureAwait(false);
        await Assert.That(runner.Runs).IsEmpty();
    }

    // A tool the CLI refuses to update stops the run: the tools after it are not updated either.
    [Test]
    public async Task UpdateAsync_WhenAnUpdateFails_StopsThere()
    {
        using var home = new TempHome();
        var runner = new FakeProcessRunner
        {
            OnRun = static (_, args) => new ProcessResult(string.Join(' ', args), 1, string.Empty, "boom", TimeSpan.Zero),
        };

        var pins = new[] { Moving("ngbv", "0.5.1", "0.6.0"), Moving("bv", "2.1.0", "2.2.0") };
        var exception = await Assert.That(async () => await UpdateAsync(home, runner, pins).ConfigureAwait(false))
            .Throws<BuildFailedException>();
        await Assert.That(exception!.ExitCode).IsEqualTo(ExitCodes.ExternalProgramFailed);
        await Assert.That(runner.Runs.Count).IsEqualTo(1);
    }

    private static PackageUpdatePolicy Policy()
    {
        _ = PackageUpdatePolicy.TryParse("minor", out var policy);
        return policy;
    }

    private static PinResolution Moving(string id, string from, string to)
        => new()
        {
            Pin = DependencyPin.Create(DependencyScope.Tools, id, from, ".config/dotnet-tools.json"),
            Policy = Policy(),
            State = PinResolutionState.Updated,
            Target = NuGetVersion.Parse(to),
        };

    private static Task UpdateAsync(TempHome home, FakeProcessRunner runner, params PinResolution[] pins)
        => new ToolPinUpdater(runner, home.Provider, NullReporter.Instance).UpdateAsync(pins);
}
