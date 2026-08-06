// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading;
using Buildvana.Core;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.IO;
using CommunityToolkit.Diagnostics;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace Buildvana.Tool.Services.Solution;

/// <summary>
/// Locates the solution file in the home directory of the current build (preferring <c>*.slnx</c>
/// over <c>*.sln</c>), parses it, and returns a <see cref="SolutionContext"/>.
/// </summary>
internal sealed class HomeDirectorySolutionContextFactory : ISolutionContextFactory
{
    private readonly IHomeDirectoryProvider _home;

    public HomeDirectorySolutionContextFactory(IHomeDirectoryProvider home)
    {
        Guard.IsNotNull(home);
        _home = home;
    }

    /// <inheritdoc/>
    public SolutionContext Create()
    {
        var directory = _home.HomeDirectory;
        var path = FindSolutionFile(directory)
            ?? throw new BuildFailedException($"Cannot find a solution file in '{directory}'.");

        var serializer = SolutionSerializers.GetSerializerByMoniker(path)
            ?? throw new BuildFailedException($"No serializer supports solution file '{path}'.");

        // The package's serializers read the file synchronously; sync-waiting on the returned task is fine here.
        var model = serializer.OpenAsync(path, CancellationToken.None).GetAwaiter().GetResult();

        return new SolutionContext(path, model);
    }

    private static string? FindSolutionFile(string directory)
    {
        var candidates = UserDirectory.EnumerateFiles(directory, "*.slnx");
        if (candidates.Count == 0)
        {
            candidates = UserDirectory.EnumerateFiles(directory, "*.sln");
        }

        return candidates.Count > 0 ? candidates[0] : null;
    }
}
