// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Tool.Infrastructure;
using CommunityToolkit.Diagnostics;
using JetBrains.Annotations;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Git;

/// <summary>
/// Implements <see cref="IGitService"/> over LibGit2Sharp.
/// </summary>
[PublicAPI]
internal sealed class GitService : IGitService, IDisposable
{
    private readonly IReporter _reporter;
    private readonly IHomeDirectoryProvider _home;
    private readonly Repository _repository;

    public GitService(IReporter reporter, IHomeDirectoryProvider home)
    {
        Guard.IsNotNull(reporter);
        Guard.IsNotNull(home);
        _reporter = reporter;
        _home = home;
        var homeDirectory = home.HomeDirectory;
        BuildFailedException.ThrowIfNot(Repository.IsValid(homeDirectory), $"There is no Git repository at {homeDirectory}");
        _repository = new Repository(homeDirectory);
        var headName = _repository.Head.CanonicalName;
        CurrentBranch = headName.StartsWith("refs/heads/", StringComparison.Ordinal) ? _repository.Head.FriendlyName : string.Empty;
    }

    /// <inheritdoc/>
    public IReadOnlyList<GitRemoteInfo> Remotes
    {
        get
        {
            var result = new List<GitRemoteInfo>();
            foreach (var remote in _repository.Network.Remotes)
            {
                using (remote)
                {
                    result.Add(new(remote.Name, remote.Url));
                }
            }

            return result;
        }
    }

    /// <inheritdoc/>
    public string CurrentBranch { get; }

    /// <inheritdoc/>
    [DisallowNull]
    public GitIdentity? CommitterIdentity
    {
        get
        {
            return field ?? GetConfiguredIdentity();
        }

        set
        {
            Guard.IsNotNull(value);
            field = value;
        }
    }

    /// <inheritdoc/>
    public GitCredentials? RemoteCredentialsFallback { get; set; }

    /// <inheritdoc/>
    public string HeadSha => _repository.Head.Tip.Sha;

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose() => _repository.Dispose();

    /// <inheritdoc/>
    public bool TagExists(string tag)
    {
        Guard.IsNotNullOrEmpty(tag);
        return _repository.Tags.Any(x => string.Equals(x.FriendlyName, tag, StringComparison.Ordinal));
    }

    /// <inheritdoc/>
    public (SemanticVersion? Latest, SemanticVersion? LatestStable) GetLatestVersions()
    {
        var versionsByCommit = GetVersionsByCommit();
        SemanticVersion? latest = null;
        foreach (var commit in _repository.Head.Commits)
        {
            if (!versionsByCommit.TryGetValue(commit.Sha, out var versions))
            {
                continue;
            }

            // A commit can carry several version tags. Its highest version and its highest stable version are the
            // ones that count.
            latest ??= versions.Max();
            var latestStable = versions.Where(static version => !version.IsPrerelease).Max();
            if (latestStable is not null)
            {
                return (latest, latestStable);
            }
        }

        return (latest, null);
    }

    /// <inheritdoc/>
    public async Task<(T Result, IReadOnlyList<string> ChangedFiles)> TrackChangesAsync<T>(Func<Task<T>> operation)
    {
        Guard.IsNotNull(operation);
        var dirtyBefore = GetDirtyFiles();
        var result = await operation().ConfigureAwait(false);
        IReadOnlyList<string> changedFiles = [.. GetDirtyFiles().Except(dirtyBefore, StringComparer.Ordinal)];
        return (result, changedFiles);
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetDirtyFiles()
    {
        var homeDirectory = _home.HomeDirectory;
        var scratchPrefix = Path.GetFullPath(CommonPaths.Scratch, homeDirectory) + Path.DirectorySeparatorChar;
        var options = new StatusOptions {
            IncludeIgnored = false,
            IncludeUntracked = true,
            RecurseUntrackedDirs = true,
        };
        return [
            .. _repository.RetrieveStatus(options)
                .Select(x => Path.GetFullPath(x.FilePath, homeDirectory))
                .Where(x => !x.StartsWith(scratchPrefix, StringComparison.Ordinal)),
        ];
    }

    /// <inheritdoc/>
    public void Stage(params string[] paths)
    {
        Guard.IsNotNull(paths);
        if (paths.Length == 0)
        {
            return;
        }

        var homeDirectory = _home.HomeDirectory;
        var pathsInRepo = paths.Select(path => {
            Guard.IsTrue(!string.IsNullOrEmpty(path), nameof(paths), "One or more paths are null or empty.");
            var absolutePath = Path.GetFullPath(path, homeDirectory);
            var pathInRepo = Path.GetRelativePath(homeDirectory, absolutePath);
            var isOutsideRepo = Path.IsPathRooted(pathInRepo)
                || pathInRepo == ".."
                || pathInRepo.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
            if (isOutsideRepo)
            {
                throw new BuildFailedException($"Git: cannot stage '{path}' because it is not in the repository.");
            }

            return pathInRepo;
        }).ToArray();

        _reporter.Detail(string.Create(CultureInfo.InvariantCulture, $"Staging {pathsInRepo.Length} file(s)..."));
        Commands.Stage(
            _repository,
            pathsInRepo,
            new StageOptions {
                IncludeIgnored = false,
                ExplicitPathsOptions = new() { ShouldFailOnUnmatchedPath = true },
            });
    }

    /// <inheritdoc/>
    public void Commit(string message, bool amend = false, bool allowEmpty = false)
    {
        var identity = CommitterIdentity;
        BuildFailedException.ThrowIf(identity is null, "Git: committer identity not set.");
        var signature = new Signature(identity.Name, identity.Email, DateTimeOffset.Now);
        var options = new CommitOptions { AmendPreviousCommit = amend, AllowEmptyCommit = allowEmpty };
        _ = _repository.Commit(message, signature, signature, options);
    }

    /// <inheritdoc/>
    public void UndoLastCommit()
    {
        _reporter.Info("Undoing last commit...");
        var previousCommit = _repository.Head.Tip.Parents.FirstOrDefault();
        BuildFailedException.ThrowIf(previousCommit is null, "Git: cannot reset, there is no commit to go back to.");
        _repository.Reset(ResetMode.Hard, previousCommit);
    }

    /// <inheritdoc/>
    public void PushBranch(string remote, bool force = false)
    {
        Guard.IsNotNullOrEmpty(remote);
        var head = _repository.Head;
        var isOnBranch = head.CanonicalName.StartsWith("refs/heads/", StringComparison.Ordinal);
        BuildFailedException.ThrowIfNot(isOnBranch, "Git: cannot push, HEAD is not on a branch.");
        if (force)
        {
            // https://stackoverflow.com/a/47295101/5753412
            // https://github.com/libgit2/libgit2sharp/blob/5085a0c617/LibGit2Sharp.Tests/PushFixture.cs#L183-L187
            // https://github.com/libgit2/libgit2sharp/issues/104#issuecomment-1553347893
            _reporter.Info($"Force pushing '{head.FriendlyName}' to '{remote}'...");
            Push(remote, $"+{head.CanonicalName}:{head.CanonicalName}");
            _reporter.Notice($"Force-pushed '{head.FriendlyName}' to '{remote}'.");
        }
        else
        {
            _reporter.Info($"Pushing '{head.FriendlyName}' to '{remote}'...");
            Push(remote, $"{head.CanonicalName}:{head.CanonicalName}");
            _reporter.Notice($"Pushed '{head.FriendlyName}' to '{remote}'.");
        }
    }

    /// <inheritdoc/>
    public void CreateTag(string tag, string sha)
    {
        Guard.IsNotNullOrEmpty(tag);
        Guard.IsNotNullOrEmpty(sha);
        _ = _repository.Tags.Add(tag, sha);
        _reporter.Notice($"Created tag '{tag}' at {sha}.");
    }

    /// <inheritdoc/>
    public void PushTag(string remote, string tag)
    {
        Guard.IsNotNullOrEmpty(remote);
        Guard.IsNotNullOrEmpty(tag);
        var tagRef = $"refs/tags/{tag}";
        var localTarget = _repository.Refs[tagRef]?.TargetIdentifier;
        BuildFailedException.ThrowIf(localTarget is null, $"Git: cannot push tag '{tag}', because it does not exist.");

        // LibGit2Sharp moves a remote tag forward as if it were a branch, where Git refuses to update a tag without
        // force. Asking the remote first gives a tag the treatment Git gives it.
        _reporter.Info($"Looking for tag '{tag}' on '{remote}'...");
        var remoteTarget = ListRemoteReferences(remote)
            .FirstOrDefault(x => string.Equals(x.CanonicalName, tagRef, StringComparison.Ordinal))
            ?.TargetIdentifier;
        if (remoteTarget is not null)
        {
            BuildFailedException.ThrowIfNot(
                string.Equals(remoteTarget, localTarget, StringComparison.Ordinal),
                $"Git: cannot push tag '{tag}', because '{remote}' has a tag of the same name at {remoteTarget}.");
            _reporter.Notice($"Tag '{tag}' is already on '{remote}'.");
            return;
        }

        _reporter.Info($"Pushing tag '{tag}' to '{remote}'...");
        Push(remote, $"{tagRef}:{tagRef}");
        _reporter.Notice($"Pushed tag '{tag}' to '{remote}'.");
    }

    /// <inheritdoc/>
    public void DeleteRemoteTag(string remote, string tag)
    {
        Guard.IsNotNullOrEmpty(remote);
        Guard.IsNotNullOrEmpty(tag);
        _reporter.Info($"Deleting tag '{tag}' from '{remote}'...");
        Push(remote, $":refs/tags/{tag}");
        _reporter.Notice($"Deleted tag '{tag}' from '{remote}'.");
    }

    /// <inheritdoc/>
    public void DeleteTag(string tag)
    {
        Guard.IsNotNullOrEmpty(tag);
        _repository.Tags.Remove(tag);
        _reporter.Notice($"Deleted local tag '{tag}'.");
    }

    /// <inheritdoc/>
    public void FetchTags(string remote)
    {
        Guard.IsNotNullOrEmpty(remote);
        using var gitRemote = GetRemote(remote);
        _reporter.Info($"Fetching tags from '{remote}'...");
        var options = new FetchOptions { CredentialsProvider = CreateCredentialsProvider() };
        try
        {
            Commands.Fetch(_repository, gitRemote.Name, ["refs/tags/*:refs/tags/*"], options, logMessage: null);
        }
        catch (LibGit2SharpException ex)
        {
            throw new BuildFailedException($"Git: cannot fetch tags from '{remote}': {ex.Message}", ex);
        }

        _reporter.Notice($"Fetched tags from '{remote}'.");
    }

    private GitIdentity? GetConfiguredIdentity()
    {
        var signature = _repository.Config.BuildSignature(DateTimeOffset.Now);
        return signature is null ? null : new(signature.Name, signature.Email);
    }

    private Dictionary<string, List<SemanticVersion>> GetVersionsByCommit()
    {
        var versionsByCommit = new Dictionary<string, List<SemanticVersion>>(StringComparer.Ordinal);
        foreach (var tag in _repository.Tags)
        {
            if (!SemanticVersion.TryParse(tag.FriendlyName, out var version))
            {
                continue;
            }

            var sha = tag.Target.Sha;
            if (!versionsByCommit.TryGetValue(sha, out var versions))
            {
                versions = [];
                versionsByCommit.Add(sha, versions);
            }

            versions.Add(version);
        }

        return versionsByCommit;
    }

    private void Push(string remote, string refSpec)
    {
        using var gitRemote = GetRemote(remote);

        // LibGit2Sharp reports a reference the remote refused through OnPushStatusError, and nowhere else:
        // without the handler, a push that the remote rejects returns as if it had succeeded.
        // The handler only collects, because an exception thrown from it would have to cross native code.
        var rejections = new List<PushStatusError>();
        var options = new PushOptions {
            CredentialsProvider = CreateCredentialsProvider(),
            OnPushStatusError = rejections.Add,
        };
        try
        {
            _repository.Network.Push(gitRemote, refSpec, options);
        }
        catch (NonFastForwardException ex)
        {
            // LibGit2Sharp refuses a push that is not a fast-forward before the remote sees it.
            var destination = refSpec[(refSpec.IndexOf(':', StringComparison.Ordinal) + 1)..];
            throw new BuildFailedException(
                $"Git: cannot push '{destination}' to '{remote}', because the remote has commits that the local reference lacks.",
                ex);
        }
        catch (LibGit2SharpException ex)
        {
            throw new BuildFailedException($"Git: cannot push to '{remote}': {ex.Message}", ex);
        }

        if (rejections.Count > 0)
        {
            var details = string.Join("; ", rejections.Select(x => $"{x.Reference}: {x.Message}"));
            throw new BuildFailedException($"Git: '{remote}' rejected the push. {details}");
        }
    }

    private IReadOnlyList<Reference> ListRemoteReferences(string remote)
    {
        using var gitRemote = GetRemote(remote);
        var credentialsProvider = CreateCredentialsProvider();
        try
        {
            var references = credentialsProvider is null
                ? _repository.Network.ListReferences(gitRemote)
                : _repository.Network.ListReferences(gitRemote, credentialsProvider);
            return [.. references];
        }
        catch (LibGit2SharpException ex)
        {
            throw new BuildFailedException($"Git: cannot list the references of '{remote}': {ex.Message}", ex);
        }
    }

    private Remote GetRemote(string name)
    {
        var remote = _repository.Network.Remotes[name];
        BuildFailedException.ThrowIf(remote is null, $"Git: there is no remote named '{name}'.");
        return remote;
    }

    private CredentialsHandler? CreateCredentialsProvider()
    {
        var credentials = RemoteCredentialsFallback;
        return credentials is null
            ? null
            : (_, _, _) => new UsernamePasswordCredentials { Username = credentials.Username, Password = credentials.Password };
    }
}
