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
            var signature = _repository.Config.BuildSignature(DateTimeOffset.Now);
            return signature is null ? null : new(signature.Name, signature.Email);
        }

        set
        {
            Guard.IsNotNull(value);

            _repository.Config.Set("user.name", value.Name);
            _repository.Config.Set("user.email", value.Email);
        }
    }

    /// <inheritdoc/>
    public GitCredentials? PushCredentialsFallback { get; set; }

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
        var signature = _repository.Config.BuildSignature(DateTimeOffset.Now);
        BuildFailedException.ThrowIf(signature is null, "Git: committer identity not set.");
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
    public void Push(bool force = false)
    {
        var head = _repository.Head;
        var remote = head.RemoteName;
        BuildFailedException.ThrowIf(string.IsNullOrEmpty(remote), "Git: cannot push, HEAD is not tracking any remote.");
        var pushOptions = new PushOptions();
        var pushCredentialsFallback = PushCredentialsFallback;
        if (pushCredentialsFallback is not null)
        {
            pushOptions.CredentialsProvider = (_, _, _) => new UsernamePasswordCredentials {
                Username = pushCredentialsFallback.Username,
                Password = pushCredentialsFallback.Password,
            };
        }

        if (force)
        {
            // https://stackoverflow.com/a/47295101/5753412
            // https://github.com/libgit2/libgit2sharp/blob/5085a0c617/LibGit2Sharp.Tests/PushFixture.cs#L183-L187
            // https://github.com/libgit2/libgit2sharp/issues/104#issuecomment-1553347893
            _reporter.Info($"Force pushing changes to '{remote}'...");
            var pushRefSpec = string.Format(CultureInfo.InvariantCulture, "+{0}:{0}", _repository.Head.CanonicalName);
            _repository.Network.Push(_repository.Network.Remotes[remote], pushRefSpec, pushOptions);
            _reporter.Notice($"Force-pushed '{head.FriendlyName}' to '{remote}'.");
        }
        else
        {
            _reporter.Info($"Pushing changes to '{remote}'...");
            _repository.Network.Push(head, pushOptions);
            _reporter.Notice($"Pushed '{head.FriendlyName}' to '{remote}'.");
        }
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
}
