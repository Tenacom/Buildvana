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
        BuildFailedException.ThrowIfNot(
            TryGetOriginInfo(out var origin, out var originUrl),
            "No origin remote found in the Git repository.");
        Origin = origin;
        OriginUrl = new(originUrl);
        var headName = _repository.Head.CanonicalName;
        CurrentBranch = headName.StartsWith("refs/heads/", StringComparison.Ordinal) ? _repository.Head.FriendlyName : string.Empty;
    }

    /// <inheritdoc/>
    public string Origin { get; }

    /// <inheritdoc/>
    public Uri OriginUrl { get; }

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
        var versions = _repository.Tags
            .Select(x => SemanticVersion.TryParse(x.FriendlyName, out var version)
                ? (x.Target.Sha, Version: version)
                : (Sha: null!, Version: null!))
            .Where(x => x.Sha is not null)
            .ToDictionary();

        SemanticVersion? latest = null;
        SemanticVersion? latestStable = null;
        foreach (var commit in _repository.Head.Commits)
        {
            if (!versions.TryGetValue(commit.Sha, out var version))
            {
                continue;
            }

            latest ??= version;
            if (version.IsPrerelease)
            {
                continue;
            }

            latestStable = version;
            break;
        }

        return (latest, latestStable);
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
        Commands.Fetch(_repository, gitRemote.Name, ["refs/tags/*:refs/tags/*"], options, logMessage: null);
        _reporter.Notice($"Fetched tags from '{remote}'.");
    }

    private bool TryGetOriginInfo([MaybeNullWhen(false)] out string name, [MaybeNullWhen(false)] out string url)
    {
        name = null!;
        url = null!;
        string? originName = null;
        string? originUrl = null;
        string? onlyRemoteName = null;
        string? onlyRemoteUrl = null;
        var isFirst = true;
        _reporter.Detail("Looking for origin remote...");
        foreach (var remote in _repository.Network.Remotes)
        {
            using (remote)
            {
                _reporter.Detail($"  '{remote.Name}' ({remote.Url})");
                if (remote.Name == "origin")
                {
                    originName = remote.Name;
                    originUrl = remote.Url;
                    break;
                }

                if (isFirst)
                {
                    onlyRemoteName = remote.Name;
                    onlyRemoteUrl = remote.Url;
                    isFirst = false;
                }
                else
                {
                    onlyRemoteName = null;
                    onlyRemoteUrl = null;
                }
            }
        }

        // Name and URL of "origin" if present; otherwise, name and URL of the _only_ remote.
        name = originName ?? onlyRemoteName;
        url = originUrl ?? onlyRemoteUrl;
        if (name is null || url is null)
        {
            _reporter.Detail("Origin remote not found.");
            return false;
        }

        // Remove trailing slashes and optional ".git" suffix
        url = url.TrimEnd('/');
        if (url.EndsWith(".git", StringComparison.Ordinal))
        {
            url = url[..^4];
        }

        _reporter.Detail($"Origin remote is '{name}' ({url})");
        return true;
    }

    private GitIdentity? GetConfiguredIdentity()
    {
        var signature = _repository.Config.BuildSignature(DateTimeOffset.Now);
        return signature is null ? null : new(signature.Name, signature.Email);
    }

    private void Push(string remote, string refSpec)
    {
        using var gitRemote = GetRemote(remote);

        // LibGit2Sharp reports a reference the remote refused through OnPushStatusError, and nowhere else:
        // without the handler, a push that the server rejects returns as if it had succeeded.
        // The handler only collects, because an exception thrown from it would have to cross native code.
        var rejections = new List<PushStatusError>();
        var options = new PushOptions {
            CredentialsProvider = CreateCredentialsProvider(),
            OnPushStatusError = rejections.Add,
        };
        _repository.Network.Push(gitRemote, refSpec, options);
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
        var references = credentialsProvider is null
            ? _repository.Network.ListReferences(gitRemote)
            : _repository.Network.ListReferences(gitRemote, credentialsProvider);
        return [.. references];
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
