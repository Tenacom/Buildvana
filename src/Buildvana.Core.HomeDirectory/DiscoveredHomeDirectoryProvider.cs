// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    private readonly IBuildHost _host;
    private readonly string _startDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoveredHomeDirectoryProvider"/> class.
    /// </summary>
    /// <param name="host">The build host through which a discovery failure is reported.</param>
    /// <param name="startDirectory">The directory from which discovery should begin. Typically the current
    /// process's working directory.</param>
    public DiscoveredHomeDirectoryProvider(IBuildHost host, string startDirectory)
    {
        Guard.IsNotNull(host);
        Guard.IsNotNullOrEmpty(startDirectory);
        _host = host;
        _startDirectory = startDirectory;
    }

    /// <inheritdoc />
    protected override string Resolve()
        => HomeDirectoryDiscovery.TryDiscover(_startDirectory, out var homeDirectory)
            ? homeDirectory
            : _host.Fail<string>($"Home directory not defined (no .buildvana-home, .git, or .git/HEAD found above '{_startDirectory}').");
}
