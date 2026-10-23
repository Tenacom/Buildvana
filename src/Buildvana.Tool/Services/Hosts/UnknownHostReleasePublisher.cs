// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Tool.Services.Release;

namespace Buildvana.Tool.Services.Hosts;

/// <summary>
/// The release publisher of an <see cref="UnknownHost"/>: it publishes nothing, and says so.
/// </summary>
/// <param name="hostName">The display name of the host.</param>
/// <param name="reporter">The reporter to report to.</param>
internal sealed class UnknownHostReleasePublisher(string hostName, IReporter reporter) : ReleasePublisher
{
    /// <inheritdoc/>
    public override Task BeginAsync() => Task.CompletedTask;

    /// <inheritdoc/>
    public override Task PublishAsync(string tag, string sha, IReadOnlyList<ReleaseAsset> assets)
    {
        reporter.Notice($"No release page created for {tag}: bv cannot publish a release on {hostName}.");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UndoAsync() => Task.CompletedTask;
}
