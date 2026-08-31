// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// One pin of an id-shaped scope: a package, a .NET local tool, or an MSBuild project SDK, as the
/// repository states it.
/// </summary>
/// <remarks>
/// <para>A pin is what one file says about one id. The same package pinned in two files is two pins, each
/// governed on its own, and a package the same file states twice — once per target framework — is two pins
/// as well, told apart by their version text.</para>
/// <para>Family pins never become instances of this record: they are filtered out as they are read, so that
/// no later step can act on one. <c>bv self-update</c> is the one command that moves them.</para>
/// </remarks>
internal sealed record DependencyPin
{
    /// <summary>Gets the scope the pin belongs to.</summary>
    public required DependencyScope Scope { get; init; }

    /// <summary>Gets the package id.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the version text, exactly as the declaring file states it.</summary>
    public required string VersionText { get; init; }

    /// <summary>Gets the form <see cref="VersionText"/> takes.</summary>
    public required PinVersionForm Form { get; init; }

    /// <summary>
    /// Gets the version, when <see cref="Form"/> is <see cref="PinVersionForm.Literal"/>; otherwise,
    /// <see langword="null"/>.
    /// </summary>
    public NuGetVersion? Version { get; init; }

    /// <summary>
    /// Gets the path of the file that declares the pin, relative to the home directory, with forward
    /// slashes: what the report groups by, and what an update would edit.
    /// </summary>
    public required string DeclaringFile { get; init; }

    /// <summary>
    /// Gets the target framework whose evaluation declares the pin, or <see langword="null"/> when the pin
    /// does not depend on one.
    /// </summary>
    public string? TargetFramework { get; init; }

    /// <summary>
    /// Gets the MSBuild item type the pin is declared as, or <see langword="null"/> for a pin that is not an
    /// MSBuild item.
    /// </summary>
    public string? ItemType { get; init; }

    /// <summary>
    /// Gets the policy the pin states for itself through <c>UpdatePolicy</c> metadata, or
    /// <see langword="null"/> when it states none. Only the <c>packages</c> scope has a carrier for it.
    /// </summary>
    public string? MetadataPolicy { get; init; }

    /// <summary>
    /// Gets the caption of the additional pin group the pin belongs to, or <see langword="null"/> for a pin
    /// that belongs to none.
    /// </summary>
    public string? GroupCaption { get; init; }

    /// <summary>
    /// Creates a pin, reading the form and the version out of the version text.
    /// </summary>
    /// <param name="scope">The scope the pin belongs to.</param>
    /// <param name="id">The package id.</param>
    /// <param name="versionText">The version text, as the declaring file states it.</param>
    /// <param name="declaringFile">The path of the declaring file, relative to the home directory.</param>
    /// <returns>The pin.</returns>
    public static DependencyPin Create(DependencyScope scope, string id, string versionText, string declaringFile)
    {
        var form = PinVersion.Read(versionText, out var version);
        return new DependencyPin
        {
            Scope = scope,
            Id = id,
            VersionText = versionText,
            Form = form,
            Version = version,
            DeclaringFile = declaringFile,
        };
    }
}
