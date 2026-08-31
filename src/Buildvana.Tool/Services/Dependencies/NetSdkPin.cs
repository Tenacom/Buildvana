// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// The .NET SDK baseline a repository pins: the <c>sdk.version</c> of <c>global.json</c>, and the
/// <c>sdk.allowPrerelease</c> setting that must agree with the scope's policy.
/// </summary>
/// <remarks>
/// <para>The scope has no id and no second pin: one repository states one baseline. A repository that
/// states none has no pin at all, which the report says and nothing creates.</para>
/// </remarks>
internal sealed record NetSdkPin
{
    /// <summary>Gets the version text, exactly as <c>global.json</c> states it.</summary>
    public required string VersionText { get; init; }

    /// <summary>Gets whether <c>bv</c> manages the pin, and what stops it when it does not.</summary>
    public required PinManagement Management { get; init; }

    /// <summary>
    /// Gets the version, when <see cref="VersionText"/> states exactly one; otherwise,
    /// <see langword="null"/>.
    /// </summary>
    public NuGetVersion? Version { get; init; }

    /// <summary>
    /// Gets the value of <c>sdk.allowPrerelease</c>, or <see langword="null"/> when <c>global.json</c> does
    /// not state it. The setting is derived state: it must say what the scope's policy says about
    /// prerelease versions, and a repository where the two disagree has pending work.
    /// </summary>
    public bool? AllowPrerelease { get; init; }

    /// <summary>
    /// Creates a pin, reading the version and what may be done with it out of the version text.
    /// </summary>
    /// <param name="versionText">The version text, as <c>global.json</c> states it.</param>
    /// <param name="allowPrerelease">The value of <c>sdk.allowPrerelease</c>, or <see langword="null"/>.</param>
    /// <returns>The pin.</returns>
    public static NetSdkPin Create(string versionText, bool? allowPrerelease)
    {
        var management = PinVersion.Read(versionText, out var version);
        return new NetSdkPin
        {
            VersionText = versionText,
            Management = management,
            Version = version,
            AllowPrerelease = allowPrerelease,
        };
    }
}
