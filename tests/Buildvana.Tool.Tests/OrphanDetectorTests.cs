// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.Dependencies;
using Buildvana.Core.Testing;
using Buildvana.Tool.Services;
using Buildvana.Tool.Services.Dependencies;
using Buildvana.Tool.Services.Solution;

internal sealed class OrphanDetectorTests
{
    private const string ProjectPath = "src/Test/Test.csproj";
    private const string AssetsPath = "src/Test/obj/project.assets.json";
    private const string CentralPinFileName = "Directory.Packages.props";

    // Alpha is in the graph, but the project states no reference to it: it is there because something else
    // pulls it in, and a central pin of it binds nothing.
    [Test]
    public async Task DetectAsync_NamesAPinTheProjectsDoNotReference()
    {
        using var home = NewHome(new AssetsFile().Resolves("Alpha", "1.0.0").Resolves("Beta", "2.0.0", direct: true));

        var orphans = await DetectAsync(Inventory(home, ["Alpha", "Beta"])).ConfigureAwait(false);

        await Assert.That(orphans.Select(static pin => pin.Id)).IsEquivalentTo(["Alpha"]);
    }

    // NuGet compares package ids without regard to case, and so does the reference the assets file states.
    [Test]
    public async Task DetectAsync_MatchesReferencesWithoutRegardToCase()
    {
        using var home = NewHome(new AssetsFile().Resolves("Alpha", "1.0.0", direct: true));

        var orphans = await DetectAsync(Inventory(home, ["ALPHA"])).ConfigureAwait(false);

        await Assert.That(orphans).IsEmpty();
    }

    // A versionless directive resolves through central package management, so the pin it resolves through is
    // in use however little the solution's own projects say about it.
    [Test]
    public async Task DetectAsync_SparesAPinAVersionlessDirectiveNames()
    {
        using var home = NewHome(new AssetsFile().Resolves("Alpha", "1.0.0"));

        var orphans = await DetectAsync(Inventory(home, ["Alpha"], directiveReferences: ["Alpha"])).ConfigureAwait(false);

        await Assert.That(orphans).IsEmpty();
    }

    // A project multi-targets, and a reference stated under one framework alone is a reference all the same:
    // the pin answers for the whole project, and removing it would break that framework.
    [Test]
    public async Task DetectAsync_SparesAPinReferencedUnderOneFrameworkAlone()
    {
        var assets = new AssetsFile()
            .Resolves("Alpha", "1.0.0", direct: true)
            .Resolves("Beta", "2.0.0", direct: true, targetFramework: "netstandard2.0");

        using var home = NewHome(assets);

        var orphans = await DetectAsync(Inventory(home, ["Alpha", "Beta"])).ConfigureAwait(false);

        await Assert.That(orphans).IsEmpty();
    }

    // Transitive pinning is the mechanism whereby a pin raises the version of a package no project
    // references, so under it a package the graph resolves at all is one its pin binds.
    [Test]
    public async Task DetectAsync_WithTransitivePinning_SparesAPinTheGraphResolves()
    {
        using var home = NewHome(new AssetsFile().PinsTransitively().Resolves("Alpha", "1.0.0"));

        var orphans = await DetectAsync(Inventory(home, ["Alpha"])).ConfigureAwait(false);

        await Assert.That(orphans).IsEmpty();
    }

    // Transitive pinning does not spare every pin. One whose package the graph never resolves raises nothing
    // there either.
    [Test]
    public async Task DetectAsync_WithTransitivePinning_NamesAPinNoGraphResolves()
    {
        using var home = NewHome(new AssetsFile().PinsTransitively().Resolves("Beta", "2.0.0", direct: true));

        var orphans = await DetectAsync(Inventory(home, ["Alpha", "Beta"])).ConfigureAwait(false);

        await Assert.That(orphans.Select(static pin => pin.Id)).IsEquivalentTo(["Alpha"]);
    }

    // A PackageReference is the reference itself, so no pin of that shape can be an orphan. Nothing is
    // restored either: the question is answered before a restore could be worth its cost.
    [Test]
    public async Task DetectAsync_WithNoCentralPin_RestoresNothingAndNamesNothing()
    {
        using var home = NewHome(new AssetsFile().Resolves("Alpha", "1.0.0"));
        var restorer = new FakeDependencyRestorer();

        var orphans = await DetectAsync(Inventory(home, ["Alpha"], itemType: "PackageReference"), restorer).ConfigureAwait(false);

        await Assert.That(orphans).IsEmpty();
        await Assert.That(restorer.Restores).IsEmpty();
    }

    [Test]
    public async Task DetectAsync_WithNoProjectStatingItsGraph_RestoresNothingAndNamesNothing()
    {
        using var home = NewHome(new AssetsFile().Resolves("Alpha", "1.0.0"));
        var restorer = new FakeDependencyRestorer();

        var orphans = await DetectAsync(Inventory(home, ["Alpha"], evaluated: false), restorer).ConfigureAwait(false);

        await Assert.That(orphans).IsEmpty();
        await Assert.That(restorer.Restores).IsEmpty();
    }

    // The override files hold bv's own references. A promotion that made a pin look alive would keep it alive
    // for good, so the restore that answers this question leaves those files out of the evaluation.
    [Test]
    public async Task DetectAsync_RestoresOnceWithTheOverrideFilesSuppressed()
    {
        using var home = NewHome(new AssetsFile().Resolves("Alpha", "1.0.0"));
        var restorer = new FakeDependencyRestorer();

        _ = await DetectAsync(Inventory(home, ["Alpha"]), restorer).ConfigureAwait(false);

        await Assert.That(restorer.Restores).IsEquivalentTo([true]);
    }

    // The detection restore rewrites every graph without the override files, and a caller that writes none
    // of them back would leave the repository building against a graph the overrides exist to correct.
    [Test]
    public async Task DetectAsync_AskedToRestoreAfterwards_PutsTheOverrideFilesBack()
    {
        using var home = NewHome(new AssetsFile().Resolves("Alpha", "1.0.0"));
        var restorer = new FakeDependencyRestorer();

        _ = await DetectAsync(Inventory(home, ["Alpha"]), restorer, restoreOverridesAfterwards: true).ConfigureAwait(false);

        await Assert.That(restorer.Restores).IsEquivalentTo([true, false]);
    }

    // Nothing was suppressed, so there is nothing to put back.
    [Test]
    public async Task DetectAsync_AskedToRestoreAfterwardsWithNoCentralPin_RestoresNothing()
    {
        using var home = NewHome(new AssetsFile().Resolves("Alpha", "1.0.0"));
        var restorer = new FakeDependencyRestorer();
        var inventory = Inventory(home, ["Alpha"], itemType: "PackageReference");

        _ = await DetectAsync(inventory, restorer, restoreOverridesAfterwards: true).ConfigureAwait(false);

        await Assert.That(restorer.Restores).IsEmpty();
    }

    [Test]
    public async Task DetectAsync_WithARestoreThatFailedForAReasonOfItsOwn_ReportsAFailedStep()
    {
        using var home = NewHome(new AssetsFile().Resolves("Alpha", "1.0.0").Reports("NU1101", "Contoso.Widgets", "Error"));

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        var exception = await Assert.That(async () => await DetectAsync(Inventory(home, ["Alpha"])).ConfigureAwait(false))
            .Throws<BuildFailedException>();

        await Assert.That(exception!.ExitCode).IsEqualTo(ExitCodes.ExternalProgramFailed);
    }

    // Audit findings are errors under TreatWarningsAsErrors, and the restore wrote every graph all the same.
    [Test]
    public async Task DetectAsync_WithAnAuditFindingAsAnError_AnswersAllTheSame()
    {
        using var home = NewHome(new AssetsFile().Resolves("Alpha", "1.0.0").Reports("NU1902", "Alpha", "Error"));

        var orphans = await DetectAsync(Inventory(home, ["Alpha"])).ConfigureAwait(false);

        await Assert.That(orphans.Select(static pin => pin.Id)).IsEquivalentTo(["Alpha"]);
    }

    // NU1900 says a source could not be read in full, which stops the override lifecycle. What a project
    // references does not depend on vulnerability data, so it does not stop this.
    [Test]
    public async Task DetectAsync_WithASourceTheRestoreCouldNotRead_AnswersAllTheSame()
    {
        using var home = NewHome(new AssetsFile().Resolves("Alpha", "1.0.0").Reports("NU1900"));

        var orphans = await DetectAsync(Inventory(home, ["Alpha"])).ConfigureAwait(false);

        await Assert.That(orphans.Select(static pin => pin.Id)).IsEquivalentTo(["Alpha"]);
    }

    private static TempHome NewHome(AssetsFile assets)
    {
        var home = new TempHome();
        home.WriteFile(ProjectPath, "<Project />");
        home.WriteFile(AssetsPath, assets.ToString());
        return home;
    }

    private static DependencyInventory Inventory(
        TempHome home,
        IReadOnlyList<string> pinnedIds,
        IReadOnlyList<string>? directiveReferences = null,
        string itemType = "PackageVersion",
        bool evaluated = true)
        => new()
        {
            Packages = [.. pinnedIds.Select(id => Pin(id, itemType))],
            DirectiveReferences = directiveReferences ?? [],
            Evaluations = evaluated ? [Evaluation(home)] : [],
        };

    private static DependencyPin Pin(string id, string itemType)
        => DependencyPin.Create(DependencyScope.Packages, id, "1.0.0", CentralPinFileName) with { ItemType = itemType };

    private static PackagePinDump Evaluation(TempHome home)
        => new()
        {
            ProjectFullPath = home.GetFullPath(ProjectPath),
            ProjectAssetsFile = home.GetFullPath(AssetsPath),
            TargetFramework = "net10.0",
            ManagePackageVersionsCentrally = true,
            NuGetAuditLevel = "low",
            Items = [],
        };

    private static Task<IReadOnlyList<DependencyPin>> DetectAsync(
        DependencyInventory inventory,
        IDependencyRestorer? restorer = null,
        bool restoreOverridesAfterwards = false)
    {
        // The restorer is the only thing that would use the solution, and it is faked.
        var solution = new Lazy<SolutionContext>(static () => null!);
        var detector = new OrphanDetector(solution, restorer ?? new FakeDependencyRestorer(), new CaptureReporter());
        return detector.DetectAsync(inventory, restoreOverridesAfterwards);
    }
}
