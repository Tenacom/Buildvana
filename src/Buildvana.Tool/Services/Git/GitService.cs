// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Buildvana.Tool.Utilities;
using Cake.Common.Diagnostics;
using Cake.Core;
using Cake.Core.IO;
using CommunityToolkit.Diagnostics;
using LibGit2Sharp;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Git;

/// <summary>
/// Provides shortcut methods to use Git.
/// </summary>
public sealed class GitService : IDisposable
{
    private readonly ICakeContext _context;
    private readonly Repository _repository;

    public GitService(ICakeContext context, OptionsService options)
    {
        Guard.IsNotNull(context);
        Guard.IsNotNull(options);
        _context = context;
        var workingDirectory = context.Environment.WorkingDirectory.FullPath;
        _context.Ensure(Repository.IsValid(workingDirectory), $"There is no Git repository at {workingDirectory}");
        _repository = new Repository(workingDirectory);
        _context.Ensure(TryGetOriginInfo(out var origin, out var originUrl), "No origin remote found in the Git repository.");
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
    public void Stage(params FilePath[] paths)
    {
        Guard.IsNotNull(paths);
        if (paths.Length == 0)
        {
            return;
        }

        var pathsInRepo = paths.Select(path =>
        {
            Guard.IsTrue(path is not null, nameof(paths), "One or more paths are null.");
            var absolutePath = path.MakeAbsolute(_context.Environment);
            var pathInRepo = _context.Environment.WorkingDirectory.GetRelativePath(absolutePath);
            if (!pathInRepo.IsRelative || pathInRepo.Segments[0] == "..")
            {
                _context.Fail($"Git: cannot stage '{path}' because it is not in the repository.");
            }

            return pathInRepo.ToString();
        }).ToArray();

        _context.Verbose($"Git: staging {pathsInRepo.Length} file(s)...");
        Commands.Stage(_repository, pathsInRepo, new StageOptions() { IncludeIgnored = false, ExplicitPathsOptions = new() { ShouldFailOnUnmatchedPath = true } });
    }

    /// <summary>
    /// Commits staged changes, or amends last commit.
    /// </summary>
    /// <param name="message">The commit message.</param>
    /// <param name="amend">If <see langword="true"/>, amends last commit instead of creating a new commit.</param>
    public void Commit(string message, bool amend = false)
    {
        var signature = _repository.Config.BuildSignature(DateTimeOffset.Now);
        _context.Ensure(signature is not null, "Git: committer identity not set.");
        var options = new CommitOptions() { AmendPreviousCommit = amend };
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
        _context.Information("Git: undoing last commit...");
        var previousCommit = _repository.Head.Tip.Parents.FirstOrDefault();
        _context.Ensure(previousCommit is not null, "Git: cannot reset, there is no commit to go back to.");
        _repository.Reset(ResetMode.Hard, previousCommit);
    }

    /// <summary>
    /// Pushes changes made to HEAD to the tracked remote. Fails if HEAD is not tracking any remote.
    /// </summary>
    public void Push(bool force = false)
    {
        var head = _repository.Head;
        var remote = head.RemoteName;
        _context.Ensure(!string.IsNullOrEmpty(remote), "Git: cannot push, HEAD is not tracking any remote.");
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
            _context.Information($"Git: force pushing changes to '{remote}'...");
            var pushRefSpec = string.Format(CultureInfo.InvariantCulture, "+{0}:{0}", _repository.Head.CanonicalName);
            _repository.Network.Push(_repository.Network.Remotes[remote], pushRefSpec, pushOptions);
        }
        else
        {
            _context.Information($"Git: pushing changes to '{remote}'...");
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
        _context.Verbose("Git: looking for origin remote...");
        foreach (var remote in _repository.Network.Remotes)
        {
            using (remote)
            {
                _context.Verbose($"Git:     '{remote.Name}' ({remote.Url})");
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
            _context.Verbose("Git: origin remote not found.");
            return false;
        }

        // Remove trailing slashes and optional ".git" suffix
        url = url.TrimEnd('/');
        if (url.EndsWith(".git", StringComparison.Ordinal))
        {
            url = url[..^4];
        }

        _context.Verbose($"Git: origin remote is '{name}' ({url})");
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
            _context.Verbose($"Git: looking for main branch on remote '{origin}' (configured value is '{configuredMainBranch}')...");
            configuredValue = $"{origin}/{configuredMainBranch}";
        }
        else
        {
            _context.Verbose($"Git: looking for main branch on remote '{origin}' (no configured value)...");
        }

        foreach (var branch in _repository.Branches.Select(static x => x.FriendlyName))
        {
            if (haveConfiguredMainBranch && branch == configuredValue)
            {
                _context.Verbose($"Git:     '{branch}' <-- configured value");
                mainBranchFound = true;
            }
            else
            {
                _context.Verbose($"Git:     '{branch}'");
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
            _context.Verbose("Git: main branch not found on remote '{origin}'.");
            return string.Empty;
        }

        _context.Verbose($"Git: main branch '{mainBranch}' found on remote '{origin}'.");
        return mainBranch;
    }
}
