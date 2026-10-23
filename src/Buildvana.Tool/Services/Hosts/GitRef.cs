// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Hosts;

/// <summary>
/// Names a tag, a branch, or a commit of a repository, together with its kind.
/// </summary>
/// <remarks>
/// <para>The kind travels with the name because some hosts put it in the URL of a file, and a name alone
/// cannot tell a tag from a branch of the same name.</para>
/// </remarks>
internal sealed record GitRef
{
    private GitRef(GitRefKind kind, string name)
    {
        Kind = kind;
        Name = name;
    }

    /// <summary>
    /// Gets the kind of the reference.
    /// </summary>
    public GitRefKind Kind { get; }

    /// <summary>
    /// Gets the name of the tag or the branch, or the SHA of the commit.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Creates a reference to a tag.
    /// </summary>
    /// <param name="name">The name of the tag.</param>
    /// <returns>A newly-created <see cref="GitRef"/>.</returns>
    public static GitRef Tag(string name)
    {
        Guard.IsNotNullOrEmpty(name);
        return new(GitRefKind.Tag, name);
    }

    /// <summary>
    /// Creates a reference to a branch.
    /// </summary>
    /// <param name="name">The name of the branch.</param>
    /// <returns>A newly-created <see cref="GitRef"/>.</returns>
    public static GitRef Branch(string name)
    {
        Guard.IsNotNullOrEmpty(name);
        return new(GitRefKind.Branch, name);
    }

    /// <summary>
    /// Creates a reference to a commit.
    /// </summary>
    /// <param name="sha">The SHA of the commit.</param>
    /// <returns>A newly-created <see cref="GitRef"/>.</returns>
    public static GitRef Commit(string sha)
    {
        Guard.IsNotNullOrEmpty(sha);
        return new(GitRefKind.Commit, sha);
    }
}
