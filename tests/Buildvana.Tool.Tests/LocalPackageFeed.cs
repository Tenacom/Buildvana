// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Compression;

/// <summary>
/// Writes a local folder feed: the package source that costs no network and that NuGet reads with the same
/// client libraries it reads a server with.
/// </summary>
/// <remarks>
/// <para>A package is the least a feed will accept: a zip holding one nuspec. Nothing under test reads a
/// package's content, only what a source says it has.</para>
/// </remarks>
internal static class LocalPackageFeed
{
    /// <summary>
    /// Writes one package into a folder feed, creating the folder when it is not there.
    /// </summary>
    /// <param name="feedPath">The path of the folder feed.</param>
    /// <param name="id">The package id.</param>
    /// <param name="version">The package version.</param>
    public static void WritePackage(string feedPath, string id, string version)
    {
        _ = Directory.CreateDirectory(feedPath);
        var nuspec = $"""
                      <?xml version="1.0" encoding="utf-8"?>
                      <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
                        <metadata>
                          <id>{id}</id>
                          <version>{version}</version>
                          <authors>Buildvana</authors>
                          <description>A package that exists.</description>
                        </metadata>
                      </package>
                      """;

        using var archive = ZipFile.Open(Path.Combine(feedPath, $"{id}.{version}.nupkg"), ZipArchiveMode.Create);
        using var writer = new StreamWriter(archive.CreateEntry($"{id}.nuspec").Open());
        writer.Write(nuspec);
    }
}
