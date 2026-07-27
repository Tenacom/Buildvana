// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using LibGit2Sharp;

internal sealed class TempGitRepo : IDisposable
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Repository _repository;

    public TempGitRepo()
    {
        RootPath = Directory.CreateTempSubdirectory("bv-test-repo-").FullName;
        _ = Repository.Init(RootPath);
        _repository = new Repository(RootPath);
    }

    public string RootPath { get; }

    public string CurrentBranchName => _repository.Head.FriendlyName;

    public string HeadSha => _repository.Head.Tip.Sha;

    public void WriteFile(string name, string content, Encoding? encoding = null)
        => File.WriteAllText(Path.Combine(RootPath, name), content, encoding ?? Utf8NoBom);

    public void CommitAll(string message = "Test commit")
    {
        Commands.Stage(_repository, "*");
        var signature = new Signature("Test", "test@example.com", DateTimeOffset.Now);
        _ = _repository.Commit(message, signature, signature, new CommitOptions { AllowEmptyCommit = true });
    }

    public void CheckoutNewBranch(string name)
    {
        var branch = _repository.CreateBranch(name);
        _ = Commands.Checkout(_repository, branch);
    }

    public void Checkout(string name) => _ = Commands.Checkout(_repository, _repository.Branches[name]);

    public void CheckoutDetached() => _ = Commands.Checkout(_repository, _repository.Head.Tip);

    public void Merge(string branchName)
    {
        var signature = new Signature("Test", "test@example.com", DateTimeOffset.Now);
        var options = new MergeOptions { FastForwardStrategy = FastForwardStrategy.NoFastForward };
        _ = _repository.Merge(_repository.Branches[branchName], signature, options);
    }

    public void Dispose()
    {
        _repository.Dispose();
        DeleteDirectory(RootPath);
    }

    private static void DeleteDirectory(string path)
    {
        // Git object files are read-only; clear attributes so the recursive delete succeeds on Windows.
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }
}
