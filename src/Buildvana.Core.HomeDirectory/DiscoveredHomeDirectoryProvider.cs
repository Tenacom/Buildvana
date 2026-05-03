// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.HomeDirectory;

/// <summary>
/// An <see cref="IHomeDirectoryProvider"/> that resolves the home directory by running
/// <see cref="HomeDirectoryDiscovery.TryDiscover"/> against a fixed start directory.
/// </summary>
/// <remarks>
/// <para>Discovery is deferred to first read of <see cref="HomeDirectoryProvider.HomeDirectory"/> and the result
/// is cached for the lifetime of the instance.</para>
/// </remarks>
public sealed class DiscoveredHomeDirectoryProvider : HomeDirectoryProvider
{
    private readonly string _startDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoveredHomeDirectoryProvider"/> class.
    /// </summary>
    /// <param name="startDirectory">The directory from which discovery should begin. Typically the current
    /// process's working directory.</param>
    public DiscoveredHomeDirectoryProvider(string startDirectory)
    {
        Guard.IsNotNullOrEmpty(startDirectory);
        _startDirectory = startDirectory;
    }

    /// <inheritdoc />
    protected override string Resolve()
        => HomeDirectoryDiscovery.TryDiscover(_startDirectory, out var homeDirectory)
            ? homeDirectory
            : throw new BuildFailedException($"Home directory not defined (no .buildvana-home, .git, or .git/HEAD found above '{_startDirectory}').");
}
