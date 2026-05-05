// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using Buildvana.Core;
using Buildvana.Core.HomeDirectory;
using CommunityToolkit.Diagnostics;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Git;

/// <summary>
/// Provides shortcut methods to use Git.
/// </summary>
public sealed class GitService : IDisposable
{
    private readonly ILogger<GitService> _logger;
    private readonly IHomeDirectoryProvider _home;
    private readonly Repository _repository;

    public GitService(ILogger<GitService> logger, IHomeDirectoryProvider home, OptionsService options)
    {
        Guard.IsNotNull(logger);
        Guard.IsNotNull(home);
        Guard.IsNotNull(options);
        _logger = logger;
        _home = home;
        var homeDirectory = home.HomeDirectory;
        BuildFailedException.ThrowIfNot(Repository.IsValid(homeDirectory), $"There is no Git repository at {homeDirectory}");
        _repository = new Repository(homeDirectory);
        BuildFailedException.ThrowIfNot(TryGetOriginInfo(out var origin, out var originUrl), "No origin remote found in the Git repository.");
        Origin = origin;
        OriginUrl = new(originUrl);
        var headName = _repository.Head.CanonicalName;
        CurrentBranch = headName.StartsWith("refs/heads/", StringComparison.Ordinal) ? _repository.Head.FriendlyName : string.Empty;
        var configuredMainBranch = options.GetOption("mainBranch", string.Empty);
        MainBranch = FindMainBranch(origin, configuredMainBranch);
    }

    /// <summary>
    /// Gets the name of the origin remote, i.e. either "origin" if such remote exists, or the name of the only remote if there is only one.
    /// </summary>
    public string Origin { get; }

    /// <summary>
    /// Gets the fetch URL of the origin remote.
    /// </summary>
    public Uri OriginUrl { get; }

    /// <summary>
    /// Gets the name of the main Git branch.
    /// </summary>
    /// <value>The name of the main branch.</value>
    public string MainBranch { get; }

    /// <summary>
    /// Gets the name of the current Git branch.
    /// </summary>
    /// <value>If HEAD is on a branch, the name of the branch; otherwise, the empty string.</value>
    public string CurrentBranch { get; }

    /// <summary>
    /// Gets or sets the identity of the Git committer.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the credentials used for pushing to the Git repository if ambient credentials are not sufficient.
    /// </summary>
    /// <remarks>
    /// <para>Set this property when ambient mechanisms (`http.extraheader` written by CI checkout actions, URL-embedded credentials, OS credential helpers)
    /// are absent or insufficient.</para>
    /// <para>The provided credentials are tried only after the server returns a 401 challenge to the initial push request.</para>
    /// </remarks>
    public GitCredentials? PushCredentialsFallback { get; set; }

    /// <summary>
    /// Gets the SHA of the current <c>HEAD</c> commit.
    /// </summary>
    public string HeadSha => _repository.Head.Tip.Sha;

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose() => _repository.Dispose();

    /// <summary>
    /// Tells whether a tag exists in the local Git repository.
    /// </summary>
    /// <param name="tag">The tag to check for.</param>
    /// <returns>If a tag whose name is equal to <paramref name="tag"/> exists in the repository, <see langword="true"/>;
    /// otherwise, <see langword="false"/>.</returns>
    public bool TagExists(string tag)
    {
        Guard.IsNotNullOrEmpty(tag);
        return _repository.Tags.Any(x => string.Equals(x.FriendlyName, tag, StringComparison.Ordinal));
    }

    /// <summary>
    /// Gets the latest version and the latest stable version in commit history.
    /// </summary>
    /// <returns>A tuple of the latest version and the latest stable version.</returns>
    /// <remarks>
    /// <para>If no version tag is found in commit history, this method returns a tuple of two <see langword="null"/>s.</para>
    /// <para>If no stable version tag is found in commit history, this method returns a tuple of the latest version and <see langword="null"/>.</para>
    /// </remarks>
    public (SemanticVersion? Latest, SemanticVersion? LatestStable) GetLatestVersions()
    {
        var versions = _repository.Tags
            .Select(x => SemanticVersion.TryParse(x.FriendlyName, out var version) ? (x.Target.Sha, Version: version) : (Sha: null!, Version: null!))
            .Where(x => x.Sha is not null)
            .ToDictionary();

        SemanticVersion? latest = null;
        SemanticVersion? latestStable = null;
        foreach (var commit in _repository.Head.Commits)
        {
            if (versions.TryGetValue(commit.Sha, out var version))
            {
                if (latest == null)
                {
                    latest = version;
                }

                if (!version.IsPrerelease)
                {
                    latestStable = version;
                    break;
                }
            }
        }

        return (latest, latestStable);
    }

    /// <summary>
    /// Adds one or more files to the Git index.
    /// </summary>
    /// <param name="paths">The paths of the files to add.</param>
    public void Stage(params string[] paths)
    {
        Guard.IsNotNull(paths);
        if (paths.Length == 0)
        {
            return;
        }

        var homeDirectory = _home.HomeDirectory;
        var pathsInRepo = paths.Select(path =>
        {
            Guard.IsTrue(!string.IsNullOrEmpty(path), nameof(paths), "One or more paths are null or empty.");
            var absolutePath = Path.GetFullPath(path, homeDirectory);
            var pathInRepo = Path.GetRelativePath(homeDirectory, absolutePath);
            if (Path.IsPathRooted(pathInRepo) || pathInRepo == ".." || pathInRepo.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new BuildFailedException($"Git: cannot stage '{path}' because it is not in the repository.");
            }

            return pathInRepo;
        }).ToArray();

        _logger.LogDebug("Git: staging {Count} file(s)...", pathsInRepo.Length);
        Commands.Stage(_repository, pathsInRepo, new StageOptions() { IncludeIgnored = false, ExplicitPathsOptions = new() { ShouldFailOnUnmatchedPath = true } });
    }

    /// <summary>
    /// Commits staged changes, or amends last commit.
    /// </summary>
    /// <param name="message">The commit message.</param>
    /// <param name="amend">If <see langword="true"/>, amends last commit instead of creating a new commit.</param>
    /// <param name="allowEmpty">If <see langword="true"/>, allows creating (or amending into) an empty commit.</param>
    public void Commit(string message, bool amend = false, bool allowEmpty = false)
    {
        var signature = _repository.Config.BuildSignature(DateTimeOffset.Now);
        BuildFailedException.ThrowIfNot(signature is not null, "Git: committer identity not set.");
        var options = new CommitOptions() { AmendPreviousCommit = amend, AllowEmptyCommit = allowEmpty };
        _ = _repository.Commit(message, signature, signature, options);
    }

    /// <summary>
    /// Undoes the most recent commit.
    /// </summary>
    /// <remarks>
    /// <para>This method's purpose is to undo a commit that was just generated by code and is not a merge commit.</para>
    /// <para>If the current <c>HEAD</c> has multiple parents, the behavior of this method is undefined.</para>
    /// <para>If the repository has no commits, or the current <c>HEAD</c> has no parents, this method will fail.</para>
    /// </remarks>
    public void UndoLastCommit()
    {
        _logger.LogInformation("Git: undoing last commit...");
        var previousCommit = _repository.Head.Tip.Parents.FirstOrDefault();
        BuildFailedException.ThrowIfNot(previousCommit is not null, "Git: cannot reset, there is no commit to go back to.");
        _repository.Reset(ResetMode.Hard, previousCommit);
    }

    /// <summary>
    /// Pushes changes made to HEAD to the tracked remote. Fails if HEAD is not tracking any remote.
    /// </summary>
    public void Push(bool force = false)
    {
        var head = _repository.Head;
        var remote = head.RemoteName;
        BuildFailedException.ThrowIfNot(!string.IsNullOrEmpty(remote), "Git: cannot push, HEAD is not tracking any remote.");
        var pushOptions = new PushOptions();
        var pushCredentialsFallback = PushCredentialsFallback;
        if (pushCredentialsFallback is not null)
        {
            pushOptions.CredentialsProvider = (_, _, _) => new UsernamePasswordCredentials { Username = pushCredentialsFallback.Username, Password = pushCredentialsFallback.Password };
        }

        if (force)
        {
            // https://stackoverflow.com/a/47295101/5753412
            // https://github.com/libgit2/libgit2sharp/blob/5085a0c6173cdb2a3fde205330b327a8eb0a26c4/LibGit2Sharp.Tests/PushFixture.cs#L183-L187
            // https://github.com/libgit2/libgit2sharp/issues/104#issuecomment-1553347893
            _logger.LogInformation("Git: force pushing changes to '{Remote}'...", remote);
            var pushRefSpec = string.Format(CultureInfo.InvariantCulture, "+{0}:{0}", _repository.Head.CanonicalName);
            _repository.Network.Push(_repository.Network.Remotes[remote], pushRefSpec, pushOptions);
        }
        else
        {
            _logger.LogInformation("Git: pushing changes to '{Remote}'...", remote);
            _repository.Network.Push(head, pushOptions);
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
        _logger.LogDebug("Git: looking for origin remote...");
        foreach (var remote in _repository.Network.Remotes)
        {
            using (remote)
            {
                _logger.LogDebug("Git:     '{Name}' ({Url})", remote.Name, remote.Url);
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
            _logger.LogDebug("Git: origin remote not found.");
            return false;
        }

        // Remove trailing slashes and optional ".git" suffix
        url = url.TrimEnd('/');
        if (url.EndsWith(".git", StringComparison.Ordinal))
        {
            url = url[..^4];
        }

        _logger.LogDebug("Git: origin remote is '{Name}' ({Url})", name, url);
        return true;
    }

    private string FindMainBranch(string origin, string configuredMainBranch)
    {
        var haveConfiguredMainBranch = !string.IsNullOrEmpty(configuredMainBranch);
        var mainBranchFound = false;
        var mainFound = false;
        var masterFound = false;
        var mainValue = $"{origin}/main";
        var masterValue = $"{origin}/master";
        var configuredValue = string.Empty;
        if (haveConfiguredMainBranch)
        {
            _logger.LogDebug("Git: looking for main branch on remote '{Origin}' (configured value is '{Configured}')...", origin, configuredMainBranch);
            configuredValue = $"{origin}/{configuredMainBranch}";
        }
        else
        {
            _logger.LogDebug("Git: looking for main branch on remote '{Origin}' (no configured value)...", origin);
        }

        foreach (var branch in _repository.Branches.Select(static x => x.FriendlyName))
        {
            if (haveConfiguredMainBranch && branch == configuredValue)
            {
                _logger.LogDebug("Git:     '{Branch}' <-- configured value", branch);
                mainBranchFound = true;
            }
            else
            {
                _logger.LogDebug("Git:     '{Branch}'", branch);
                if (branch == mainValue)
                {
                    mainFound = true;
                }
                else if (branch == masterValue)
                {
                    masterFound = true;
                }
            }
        }

        var mainBranch = mainBranchFound ? configuredMainBranch
            : mainFound ? "main"
            : masterFound ? "master"
            : null;

        if (mainBranch is null)
        {
            _logger.LogDebug("Git: main branch not found on remote '{Origin}'.", origin);
            return string.Empty;
        }

        _logger.LogDebug("Git: main branch '{Branch}' found on remote '{Origin}'.", mainBranch, origin);
        return mainBranch;
    }
}
