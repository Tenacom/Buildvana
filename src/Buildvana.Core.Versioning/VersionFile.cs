// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.IO;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.Versioning;

/// <summary>
/// Represents the repository's version file, for reading the version specification and applying
/// version advances.
/// </summary>
/// <remarks>
/// <para>The version file (<see cref="FileName"/>, in the home directory) is plain text holding a single
/// <c>MAJOR.MINOR[-[tag]]</c> version specification; see the <c>Parse</c> method in
/// <see cref="VersionSpecExtensions"/> for the accepted format.</para>
/// </remarks>
public sealed class VersionFile
{
    /// <summary>
    /// The name of the version file, relative to the home directory.
    /// </summary>
    public const string FileName = "VERSION";

    private VersionFile(string absolutePath, VersionSpec spec)
    {
        Path = absolutePath;
        Spec = spec;
    }

    /// <summary>
    /// Gets the absolute path of the version file.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the version specification read from the version file, as possibly modified by
    /// <see cref="ApplyChange"/>.
    /// </summary>
    public VersionSpec Spec { get; private set; }

    /// <summary>
    /// Constructs a <see cref="VersionFile"/> instance by loading the repository's version file.
    /// </summary>
    /// <param name="home">The home directory provider used to locate the version file.</param>
    /// <returns>A newly-created <see cref="VersionFile"/>, representing the loaded data.</returns>
    /// <exception cref="BuildFailedException">The version file is absent, cannot be read, or does not
    /// contain a valid version specification.</exception>
    public static VersionFile Load(IHomeDirectoryProvider home)
    {
        Guard.IsNotNull(home);
        var path = home.GetFullPath(FileName);
        return new(path, ReadSpec(path));
    }

    /// <summary>
    /// Applies a version specification change to this instance.
    /// </summary>
    /// <param name="change">A <see cref="VersionSpecChange"/> constant representing the kind of change to apply.</param>
    /// <returns>If the <see cref="Spec"/> property actually changed as a result of
    /// <paramref name="change"/>, <see langword="true"/>; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>This method does not save the modified version file; call <see cref="Save"/> for that.</para>
    /// </remarks>
    public bool ApplyChange(VersionSpecChange change)
    {
        (Spec, var changed) = Spec.ApplyChange(change);
        return changed;
    }

    /// <summary>
    /// Saves the version file, possibly with a modified <see cref="Spec"/>, back to the repository.
    /// </summary>
    /// <param name="prereleaseTag">The informational tag text to write after the prerelease marker,
    /// usually the configured <see cref="VersioningSettings.PrereleaseTag"/>. It is only written when
    /// <see cref="Spec"/> is a prerelease and the tag fits the version file grammar; otherwise a bare
    /// <c>-</c> marks the prerelease.</param>
    /// <exception cref="BuildFailedException">The version file cannot be written.</exception>
    public void Save(string? prereleaseTag = null)
    {
        var text = Format(Spec, prereleaseTag);
        UserFile.WriteAllText(Path, text);
    }

    private static VersionSpec ReadSpec(string path)
    {
        BuildFailedException.ThrowIfNot(File.Exists(path), $"Version file {path} not found.");
        var text = UserFile.ReadAllText(path).Trim();
        BuildFailedException.ThrowIfNot(
            VersionSpec.TryParse(text, out var spec),
            $"{path} contains an invalid version specification '{text}'.");
        return spec;
    }

    private static string Format(VersionSpec spec, string? prereleaseTag)
    {
        var stable = FormattableString.Invariant($"{spec.Major}.{spec.Minor}");
        if (!spec.Prerelease)
        {
            return stable + "\n";
        }

        var tagged = stable + "-" + prereleaseTag;
        var tagFits = prereleaseTag is not null && VersionSpec.TryParse(tagged, out _);
        return (tagFits ? tagged : stable + "-") + "\n";
    }
}
