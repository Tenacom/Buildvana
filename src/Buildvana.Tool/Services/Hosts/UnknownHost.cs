// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Git;

namespace Buildvana.Tool.Services.Hosts;

/// <summary>
/// A Git host <c>bv</c> does not know: it has no URL scheme, no token, and no release page.
/// </summary>
/// <param name="url">The parsed URL of the repository.</param>
/// <param name="reporter">The reporter the release publisher reports to.</param>
internal sealed class UnknownHost(GitUrlInfo url, IReporter reporter) : GitHost(url)
{
    /// <inheritdoc/>
    public override string Name => $"unknown host ({HostName})";

    /// <inheritdoc/>
    /// <value>Always <see langword="null"/>.</value>
    public override HostKind? Kind => null;

    /// <inheritdoc/>
    /// <value>Always <see langword="null"/>.</value>
    public override Uri? RepositoryUrl => null;

    /// <inheritdoc/>
    /// <value>Always <see langword="null"/>.</value>
    public override string? TokenEnv => null;

    /// <inheritdoc/>
    /// <value>Always <see langword="null"/>.</value>
    public override string? PushUsername => null;

    /// <inheritdoc/>
    /// <returns>Always <see langword="null"/>.</returns>
    public override Uri? GetReleaseUrl(string tag) => null;

    /// <inheritdoc/>
    /// <returns>Always <see langword="null"/>.</returns>
    public override Uri? GetFileUrl(string path, GitRef gitRef) => null;

    /// <inheritdoc/>
    /// <summary>
    /// This method is not supported on an unknown host and always throws.
    /// </summary>
    public override Task<bool> IsPrivateRepositoryAsync() => BuildFailedException.ThrowOnUnsupportedMethod<Task<bool>>();

    /// <inheritdoc/>
    public override ReleasePublisher CreateReleasePublisher() => new UnknownHostReleasePublisher(Name, reporter);
}
