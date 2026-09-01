// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Configuration;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Says in words what a reader must know about a pin beyond its version and its policy.
/// </summary>
/// <remarks>
/// <para>Every command says these things the same way. <c>show</c> states them offline and <c>update</c>
/// states them beside a target, so the words live here rather than in either report.</para>
/// </remarks>
internal static class PinNotes
{
    /// <summary>
    /// The note of a pin whose version the sources know and have delisted.
    /// </summary>
    public const string Delisted = "the sources have delisted this version; updating is the remedy";

    private const string PrereleaseUnderStablePolicy =
        "a prerelease under a policy that takes only stable versions; end the policy with '-' to follow the prerelease line";

    /// <summary>
    /// Says what a reader must know about a pin of an id-shaped scope.
    /// </summary>
    /// <param name="pin">The pin.</param>
    /// <param name="policy">The pin's effective policy.</param>
    /// <returns>The note, or an empty string when the pin calls for none.</returns>
    public static string For(DependencyPin pin, PackageUpdatePolicy policy)
    {
        var unmanaged = Unmanaged(pin.Management);
        return unmanaged.Length > 0 ? unmanaged : ForVersion(pin.Version, policy);
    }

    /// <summary>
    /// Says what a reader must know about a version a run states for a pin <c>bv</c> manages.
    /// </summary>
    /// <param name="version">The version, or <see langword="null"/> when there is none.</param>
    /// <param name="policy">The pin's effective policy.</param>
    /// <returns>The note, or an empty string when the version calls for none.</returns>
    /// <remarks>
    /// <para>A run that states a version of its own overrules the policy, and the note is then about the
    /// version the file ends up holding, rather than the one it held.</para>
    /// </remarks>
    public static string ForVersion(NuGetVersion? version, PackageUpdatePolicy policy)
        => version is { IsPrerelease: true } && !policy.AllowPrerelease ? PrereleaseUnderStablePolicy : string.Empty;

    /// <summary>
    /// Says what a reader must know about the .NET SDK baseline.
    /// </summary>
    /// <param name="pin">The pin.</param>
    /// <param name="policy">The scope's policy.</param>
    /// <returns>The note, or an empty string when the pin calls for none.</returns>
    /// <remarks>
    /// <para>The <c>allowPrerelease</c> setting is derived state: it must say what the policy says, and an
    /// apply run writes it. A disagreement is worth a word of its own, since nothing else shows it.</para>
    /// </remarks>
    public static string ForNetSdk(NetSdkPin pin, NetSdkUpdatePolicy policy)
    {
        var unmanaged = Unmanaged(pin.Management);
        if (unmanaged.Length > 0)
        {
            return unmanaged;
        }

        return pin.AllowPrerelease == policy.AllowPrerelease
            ? string.Empty
            : $"global.json states allowPrerelease as {Stated(pin.AllowPrerelease)}, where the policy says {policy.AllowPrerelease}";
    }

    /// <summary>
    /// Says why <c>bv</c> does not manage a pin.
    /// </summary>
    /// <param name="management">What the pin's version text makes of it.</param>
    /// <returns>The note, or an empty string for a pin <c>bv</c> manages.</returns>
    public static string Unmanaged(PinManagement management)
        => management switch
        {
            PinManagement.Managed => string.Empty,
            PinManagement.BracketExactVersion => "not managed: one version in brackets; write it without them to have bv move it",
            PinManagement.VersionRange => "not managed: a version range decides what resolves",
            PinManagement.FloatingVersion => "not managed: a floating version resolves anew at every restore",
            PinManagement.UnreadableVersion => "not managed: NuGet reads this as neither a version nor a range",
            PinManagement.VersionOverride => "not managed: VersionOverride departs from the central pin on purpose",
            _ => "not managed: the file states the version through a property, not as a literal",
        };

    private static string Stated(bool? value) => value?.ToString() ?? "unstated";
}
