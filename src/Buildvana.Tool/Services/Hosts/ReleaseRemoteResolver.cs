// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Hosts;

/// <summary>
/// Base class for the services that find the release remote: the remote of the clone that the releases are
/// pushed to.
/// </summary>
internal abstract class ReleaseRemoteResolver
{
    private protected ReleaseRemoteResolver()
    {
    }

    /// <summary>
    /// Finds the release remote.
    /// </summary>
    /// <returns>The name of the release remote.</returns>
    /// <exception cref="Core.BuildFailedException">The clone has no remote <c>bv</c> can push a release to.
    /// The message says why.</exception>
    public abstract string Resolve();
}
