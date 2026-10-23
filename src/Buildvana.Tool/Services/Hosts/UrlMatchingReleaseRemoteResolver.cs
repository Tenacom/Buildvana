// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Git;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Hosts;

/// <summary>
/// Finds the release remote as the remote whose URL names the repository that <c>repository.url</c> names.
/// </summary>
/// <remarks>
/// <para>Two URLs name the same repository when their host names are equal and their path segments are equal,
/// both case-insensitive. The scheme, the port, the user, and a <c>.git</c> suffix play no part, so that the
/// SSH URL and the HTTPS URL of a repository match.</para>
/// <para>LibGit2Sharp pushes over HTTP and HTTPS alone. A remote with another URL that matches is refused,
/// unless another remote with an HTTP or HTTPS URL matches too.</para>
/// </remarks>
internal sealed class UrlMatchingReleaseRemoteResolver : ReleaseRemoteResolver
{
    private readonly IReporter _reporter;
    private readonly IGitService _git;
    private readonly RepositoryConfig _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="UrlMatchingReleaseRemoteResolver"/> class.
    /// </summary>
    /// <param name="reporter">The reporter to report to.</param>
    /// <param name="git">The Git service.</param>
    /// <param name="config">The configuration.</param>
    public UrlMatchingReleaseRemoteResolver(IReporter reporter, IGitService git, BuildvanaConfig config)
    {
        Guard.IsNotNull(reporter);
        Guard.IsNotNull(git);
        Guard.IsNotNull(config);
        _reporter = reporter;
        _git = git;
        _repository = config.Repository;
    }

    /// <inheritdoc/>
    public override string Resolve()
    {
        var repositoryUrl = GitHost.ParseRepositoryUrl(_repository);
        var remotes = _git.Remotes;
        var matches = remotes
            .Where(x => GitUrlInfo.TryCreate(x.Url, out var info) && NameSameRepository(info, repositoryUrl))
            .ToArray();
        if (matches.Length == 0)
        {
            var list = remotes.Count == 0
                ? "The clone has no remotes."
                : "The remotes of the clone are " + string.Join(", ", remotes.Select(x => $"'{x.Name}' ({x.Url})")) + ".";
            throw new BuildFailedException($"No remote of the clone names repository.url '{_repository.Url}'. {list}");
        }

        var pushable = matches.FirstOrDefault(x => IsHttp(x.Url));
        if (pushable is null)
        {
            var match = matches[0];
            throw new BuildFailedException(
                $"Remote '{match.Name}' names repository.url, but bv cannot push to '{match.Url}': "
                + "LibGit2Sharp pushes over HTTP and HTTPS alone. Add a remote with the HTTPS URL of the repository.");
        }

        _reporter.Info($"The release remote is '{pushable.Name}' ({pushable.Url}).");
        return pushable.Name;
    }

    private static bool NameSameRepository(GitUrlInfo a, GitUrlInfo b)
        => string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase)
            && a.PathSegments.SequenceEqual(b.PathSegments, StringComparer.OrdinalIgnoreCase);

    private static bool IsHttp(string url)
        => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
}
