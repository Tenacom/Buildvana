// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;
using System.Runtime.InteropServices;
using LibGit2Sharp;

namespace Buildvana.Core.Versioning;

partial class GitHeightCalculator
{
    // LibGit2Sharp normally locates its native library through the host's dependency context (deps.json).
    // MSBuild task hosts load task assemblies without one, so when the conventional runtimes/{rid}/native
    // directory exists next to the managed assembly, point LibGit2Sharp at it explicitly.
    // This must happen before the first native call in the process, hence the static constructor: it is
    // guaranteed to run before any member of the library's only Git touchpoint is used.
    static GitHeightCalculator()
    {
        var baseDirectory = Path.GetDirectoryName(typeof(Repository).Assembly.Location);
        if (string.IsNullOrEmpty(baseDirectory))
        {
            return;
        }

        var nativeDirectory = Path.Combine(baseDirectory, "runtimes", RuntimeInformation.RuntimeIdentifier, "native");
        if (!Directory.Exists(nativeDirectory))
        {
            return;
        }

        try
        {
            GlobalSettings.NativeLibraryPath = nativeDirectory;
        }
        catch (LibGit2SharpException)
        {
            // The native library is already loaded, so resolution has evidently succeeded some other way.
        }
    }
}
