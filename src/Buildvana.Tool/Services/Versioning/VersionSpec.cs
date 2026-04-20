// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Buildvana.Tool.Services.Versioning;

/// <summary>
/// Represents a Major.Minor[-Tag] version as found in version.json.
/// </summary>
public sealed partial record VersionSpec
{
    private static readonly Regex VersionSpecRegex = GetVersionSpecRegex();

    private VersionSpec(int major, int minor, string tag)
    {
        Major = major;
        Minor = minor;
        Tag = tag;
    }

    /// <summary>
    /// Gets the major version.
    /// </summary>
    public int Major { get; }

    /// <summary>
    /// Gets the minor version.
    /// </summary>
    public int Minor { get; }

    /// <summary>
    /// Gets the current unstable tag.
    /// </summary>
    /// <value>The current unstable tag, or the empty string if the current version is stable.</value>
    public string Tag { get; }

    /// <summary>
    /// Gets a value indicating whether this instance has an unstable tag.
    /// </summary>
    public bool HasTag => !string.IsNullOrEmpty(Tag);

    /// <summary>
    /// Attempts to parse a <see cref="VersionSpec"/> from the specified string.
    /// </summary>
    /// <param name="str">The string to parse.</param>
    /// <param name="result">When this method returns <see langword="true"/>, a newly-created <see cref="VersionSpec"/>.
    /// This parameter is passed uninitialized.</param>
    /// <returns><see langword="true"/> if successful, <see langword="false"/> otherwise.</returns>
    public static bool TryParse(string str, [MaybeNullWhen(false)] out VersionSpec result)
    {
        var match = VersionSpecRegex.Match(str);
        if (!match.Success)
        {
            result = null;
            return false;
        }

        result = new(
            int.Parse(match.Groups["major"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["minor"].Value, CultureInfo.InvariantCulture),
            match.Groups["tag"].Value);

        return true;
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Major}.{Minor}{(HasTag ? "-" + Tag : null)}";

    /// <summary>
    /// Gets an instance of <see cref="VersionSpec"/> that represents the same version as the current instance and has no unstable tag.
    /// </summary>
    /// <returns>If this instance has no unstable tag, this instance; otherwise, a newly-created <see cref="VersionSpec"/>
    /// that represents the same version as the current instance and has no unstable tag.</returns>
    public VersionSpec Stable() => HasTag ? new(Major, Minor, string.Empty) : this;

    /// <summary>
    /// Gets an instance of <see cref="VersionSpec"/> that represents the same version as the current instance and has the specified unstable tag.
    /// </summary>
    /// <param name="tag">The unstable tag of the returned instance.</param>
    /// <returns>If this instance's <see cref="Tag"/> property is equal to the given <paramref name="tag"/>, this instance;
    /// otherwise, a newly-created <see cref="VersionSpec"/> that represents the same version as the current instance and has the specified unstable tag.</returns>
    public VersionSpec Unstable(string tag) => string.Equals(Tag, tag, StringComparison.Ordinal) ? this : new(Major, Minor, tag);

    /// <summary>
    /// Gets an instance of <see cref="VersionSpec"/> that represents the next minor version with respect to the current instance and has the specified unstable tag.
    /// </summary>
    /// <param name="tag">The unstable tag of the returned instance.</param>
    /// <returns>A newly-created <see cref="VersionSpec"/>.</returns>
    public VersionSpec NextMinor(string tag) => new(Major, Minor + 1, tag);

    /// <summary>
    /// Gets an instance of <see cref="VersionSpec"/> that represents the next major version with respect to the current instance and has the specified unstable tag.
    /// </summary>
    /// <param name="tag">The unstable tag of the returned instance.</param>
    /// <returns>A newly-created <see cref="VersionSpec"/>.</returns>
    public VersionSpec NextMajor(string tag) => new(Major + 1, 0, tag);

    /// <summary>
    /// Gets an instance of <see cref="VersionSpec"/> that represents the result of applying the specified change to the current instance.
    /// </summary>
    /// <param name="change">A <see cref="VersionSpecChange"/> constant representing the kind of change to apply.</param>
    /// <param name="tag">If the returned instance has an unstable tag, the unstable tag of the returned instance; otherwise, this parameter is ignored.</param>
    /// <returns>
    /// <para>A tuple of the following values:</para>
    /// <list type="table">
    ///   <item>
    ///     <term>Result</term>
    ///     <description>The result of applying <paramref name="change"/> to the current instance.</description>
    ///   </item>
    ///   <item>
    ///     <term>Changed</term>
    ///     <description>If Result is equal to the current instance, <see langword="false"/>; otherwise, <see langword="true"/>.</description>
    ///   </item>
    /// </list>
    /// </returns>
    public (VersionSpec Result, bool Changed) ApplyChange(VersionSpecChange change, string tag)
        => change switch {
            VersionSpecChange.Unstable => HasTag ? (this, false) : (Unstable(tag), true),
            VersionSpecChange.Stable => HasTag ? (Stable(), true) : (this, false),
            VersionSpecChange.Minor => (NextMinor(tag), true),
            VersionSpecChange.Major => (NextMajor(tag), true),
            _ => (this, false),
        };

    [GeneratedRegex(@"(?-imsx)^v?(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)(-(?<tag>.*))?$", RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex GetVersionSpecRegex();
}
