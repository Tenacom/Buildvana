// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Testing;
using Buildvana.Sdk.Tasks;

internal sealed class ComputeVersionTests
{
    [Test]
    public async Task Execute_WithMissingHomeDirectory_LogsError()
    {
        var engine = new RecordingBuildEngine();
        var task = new ComputeVersion { BuildEngine = engine };
        await Assert.That(task.Execute()).IsFalse();
        await Assert.That(engine.Errors.Count).IsEqualTo(1);
        await Assert.That(engine.Errors[0].Message!).Contains("BVSDK1050");
    }

    [Test]
    public async Task Execute_StableNonPublic_ComputesVersionOutputs()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "1.2\n");
        repo.CommitAll();
        var task = RunTask(repo);
        await Assert.That(task.SimpleVersion).IsEqualTo("1.2.1");
        await Assert.That(task.SemVer).IsEqualTo("1.2.1");
        await Assert.That(task.AssemblyVersion).IsEqualTo("1.0.0.0");
        await Assert.That(task.FileVersion).IsEqualTo("1.2.1.0");
        await Assert.That(task.InformationalVersion).IsEqualTo($"1.2.1-g{repo.HeadSha[..10]}");
        await Assert.That(task.IsPublicRelease).IsFalse();
        await Assert.That(task.IsPrerelease).IsFalse();
        await Assert.That(task.CommitId).IsEqualTo(repo.HeadSha);
        await Assert.That(task.Height).IsEqualTo(1);
    }

    [Test]
    public async Task Execute_WithConfigurationFile_HonorsVersioningSettings()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "1.2-x\n");
        repo.WriteFile(
            "buildvana.json",
            """{ "release": { "branches": ["^rel$"] }, "versioning": { "prereleaseTag": "preview", "assemblyVersionPrecision": "minor" } }""");
        repo.CommitAll();
        repo.CheckoutNewBranch("rel");
        var task = RunTask(repo);
        await Assert.That(task.SemVer).IsEqualTo("1.2.1-preview");
        await Assert.That(task.AssemblyVersion).IsEqualTo("1.2.0.0");
        await Assert.That(task.InformationalVersion).IsEqualTo("1.2.1-preview");
        await Assert.That(task.IsPublicRelease).IsTrue();
        await Assert.That(task.IsPrerelease).IsTrue();
    }

    [Test]
    public async Task Execute_WithUnchangedRepositoryState_UsesCachedResult()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "1.2\n");
        repo.CommitAll();
        var firstEngine = new RecordingBuildEngine();
        _ = RunTask(repo, firstEngine);
        var secondEngine = new RecordingBuildEngine();
        var second = RunTask(repo, secondEngine);

        // The versioning service logs the computed version; a cache hit computes nothing and stays silent.
        await Assert.That(firstEngine.Messages.Count(m => m.Message!.Contains("Version 1.2.1", StringComparison.Ordinal))).IsEqualTo(1);
        await Assert.That(secondEngine.Messages.Count(m => m.Message!.Contains("Version 1.2.1", StringComparison.Ordinal))).IsEqualTo(0);
        await Assert.That(second.SemVer).IsEqualTo("1.2.1");
        await Assert.That(second.Height).IsEqualTo(1);
    }

    [Test]
    public async Task Execute_AfterNewCommit_RecomputesVersion()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "1.2\n");
        repo.CommitAll();
        var first = RunTask(repo);
        await Assert.That(first.Height).IsEqualTo(1);
        repo.CommitAll();
        var engine = new RecordingBuildEngine();
        var second = RunTask(repo, engine);
        await Assert.That(second.Height).IsEqualTo(2);
        await Assert.That(second.SemVer).IsEqualTo("1.2.2");
        await Assert.That(engine.Messages.Count(m => m.Message!.Contains("Version 1.2.2", StringComparison.Ordinal))).IsEqualTo(1);
    }

    [Test]
    public async Task Execute_AfterVersionFileChange_RecomputesVersion()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "1.2\n");
        repo.CommitAll();
        var first = RunTask(repo);
        await Assert.That(first.SemVer).IsEqualTo("1.2.1");
        repo.WriteFile("VERSION", "1.3\n");
        var second = RunTask(repo);
        await Assert.That(second.SemVer).IsEqualTo("1.3.0");
        await Assert.That(second.Height).IsEqualTo(0);
    }

    private static ComputeVersion RunTask(TempGitRepo repo, RecordingBuildEngine? engine = null)
    {
        var task = new ComputeVersion
        {
            BuildEngine = engine ?? new RecordingBuildEngine(),
            HomeDirectory = repo.RootPath,
        };
        _ = task.Execute();
        return task;
    }
}
