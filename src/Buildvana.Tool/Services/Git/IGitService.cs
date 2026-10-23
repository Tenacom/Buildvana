// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Buildvana.Tool.Infrastructure;
using JetBrains.Annotations;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Git;

/// <summary>
/// Provides shortcut methods to use the Git repository of the home directory.
/// </summary>
[PublicAPI]
internal interface IGitService
{
    /// <summary>
    /// Gets the remotes of the repository, in the order the Git configuration states them.
    /// </summary>
    IReadOnlyList<GitRemoteInfo> Remotes { get; }

    /// <summary>
    /// Gets the name of the current Git branch.
    /// </summary>
    /// <value>If HEAD is on a branch, the name of the branch; otherwise, the empty string.</value>
    string CurrentBranch { get; }

    /// <summary>
    /// Gets or sets the identity of the Git committer.
    /// </summary>
    /// <value>The identity last set on this instance, if any; otherwise, the identity that the Git configuration
    /// states, or <see langword="null"/> if it states none.</value>
    /// <remarks>
    /// <para>A set identity lives in this instance alone, and nothing writes it to the Git configuration.</para>
    /// </remarks>
    [DisallowNull]
    GitIdentity? CommitterIdentity { get; set; }

    /// <summary>
    /// Gets or sets the credentials used for fetching from and pushing to a remote if ambient credentials are not sufficient.
    /// </summary>
    /// <remarks>
    /// <para>Set this property when ambient mechanisms (`http.extraheader` written by CI checkout actions,
    /// URL-embedded credentials, OS credential helpers) are absent or insufficient.</para>
    /// <para>The provided credentials are tried only after the server returns a 401 challenge to the initial request.</para>
    /// </remarks>
    GitCredentials? RemoteCredentialsFallback { get; set; }

    /// <summary>
    /// Gets the SHA of the current <c>HEAD</c> commit.
    /// </summary>
    string HeadSha { get; }

    /// <summary>
    /// Tells whether a tag exists in the local Git repository.
    /// </summary>
    /// <param name="tag">The tag to check for.</param>
    /// <returns>If a tag whose name is equal to <paramref name="tag"/> exists in the repository, <see langword="true"/>;
    /// otherwise, <see langword="false"/>.</returns>
    bool TagExists(string tag);

    /// <summary>
    /// Gets the latest version and the latest stable version in commit history.
    /// </summary>
    /// <returns>A tuple of the latest version and the latest stable version.</returns>
    /// <remarks>
    /// <para>If no version tag is found in commit history, this method returns a tuple of two <see langword="null"/>s.</para>
    /// <para>If no stable version tag is found in commit history, this method returns a tuple of the latest
    /// version and <see langword="null"/>.</para>
    /// </remarks>
    (SemanticVersion? Latest, SemanticVersion? LatestStable) GetLatestVersions();

    /// <summary>
    /// Runs an operation and detects the files it modified, by comparing working-tree snapshots
    /// (as per <see cref="GetDirtyFiles"/>) taken before and after the operation.
    /// </summary>
    /// <typeparam name="T">The type of the operation's result.</typeparam>
    /// <param name="operation">The operation to run.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the ongoing operation, whose result is a tuple
    /// of the operation's result and the absolute paths of the files the operation modified. Files that
    /// were already dirty before the operation are not reported, even if the operation modified them again.</returns>
    Task<(T Result, IReadOnlyList<string> ChangedFiles)> TrackChangesAsync<T>(Func<Task<T>> operation);

    /// <summary>
    /// Gets the files that are new, modified, or otherwise different between the working directory
    /// (including the index) and the current <c>HEAD</c> commit. Ignored files are not considered;
    /// neither are files under bv's scratch directory (<see cref="CommonPaths.Scratch"/>),
    /// whether or not the repository ignores it.
    /// </summary>
    /// <returns>The absolute paths of the differing files.</returns>
    IReadOnlyList<string> GetDirtyFiles();

    /// <summary>
    /// Adds one or more files to the Git index.
    /// </summary>
    /// <param name="paths">The paths of the files to add.</param>
    void Stage(params string[] paths);

    /// <summary>
    /// Commits staged changes, or amends last commit.
    /// </summary>
    /// <param name="message">The commit message.</param>
    /// <param name="amend">If <see langword="true"/>, amends last commit instead of creating a new commit.</param>
    /// <param name="allowEmpty">If <see langword="true"/>, allows creating (or amending into) an empty commit.</param>
    void Commit(string message, bool amend = false, bool allowEmpty = false);

    /// <summary>
    /// Undoes the most recent commit.
    /// </summary>
    /// <remarks>
    /// <para>This method's purpose is to undo a commit that was just generated by code and is not a merge commit.</para>
    /// <para>If the current <c>HEAD</c> has multiple parents, the behavior of this method is undefined.</para>
    /// <para>If the repository has no commits, or the current <c>HEAD</c> has no parents, this method will fail.</para>
    /// <para>Undoing a commit is not an outcome in itself: callers undo a whole set of commits at once, and only
    /// the caller knows what that set was, so the record of the undoing belongs to it. This method only narrates.</para>
    /// </remarks>
    void UndoLastCommit();

    /// <summary>
    /// Pushes the current branch to the branch of the same name on a remote.
    /// </summary>
    /// <param name="remote">The name of the remote.</param>
    /// <param name="force">If <see langword="true"/>, replaces the remote branch even if the push is not a fast-forward.</param>
    /// <remarks>
    /// <para>This method fails if <c>HEAD</c> is not on a branch, if the push is not a fast-forward and
    /// <paramref name="force"/> is <see langword="false"/>, and if the remote rejects the push.
    /// The tracking information of the branch plays no part in the push.</para>
    /// </remarks>
    void PushBranch(string remote, bool force = false);

    /// <summary>
    /// Creates a lightweight tag in the local Git repository.
    /// </summary>
    /// <param name="tag">The name of the tag.</param>
    /// <param name="sha">The SHA of the commit the tag points to.</param>
    void CreateTag(string tag, string sha);

    /// <summary>
    /// Pushes a tag of the local Git repository to a remote.
    /// </summary>
    /// <param name="remote">The name of the remote.</param>
    /// <param name="tag">The name of the tag.</param>
    /// <remarks>
    /// <para>As Git does, this method never moves a tag of the remote. When the remote has a tag of the same name,
    /// this method does nothing if the two tags point to the same object, and fails otherwise.</para>
    /// <para>This method also fails if the local repository has no such tag, and if the remote rejects the push.</para>
    /// </remarks>
    void PushTag(string remote, string tag);

    /// <summary>
    /// Deletes a tag from a remote.
    /// </summary>
    /// <param name="remote">The name of the remote.</param>
    /// <param name="tag">The name of the tag.</param>
    void DeleteRemoteTag(string remote, string tag);

    /// <summary>
    /// Deletes a tag from the local Git repository.
    /// </summary>
    /// <param name="tag">The name of the tag.</param>
    void DeleteTag(string tag);

    /// <summary>
    /// Fetches every tag of a remote into the local Git repository.
    /// </summary>
    /// <param name="remote">The name of the remote.</param>
    void FetchTags(string remote);
}
