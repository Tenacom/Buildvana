// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// The args handed to the <c>release/post-release</c> hook: serialized by <c>bv</c> to the hook's args
/// file before the hook runs, deserialized by the hook via <see cref="Load(string)"/>.
/// </summary>
/// <remarks>
/// <para>The contract is additive-only: newer <c>bv</c> versions may add properties, never remove or repurpose
/// existing ones. Properties added after the initial release must be optional with a default value, so that an
/// args file written before an update can still be loaded.</para>
/// </remarks>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record PostReleaseHookArgs : HookArgs, IHookEvent
{
    /// <summary>The name of the context this hook's event belongs to.</summary>
    public const string Context = "release";

    /// <summary>The name of the event that triggers this hook.</summary>
    public const string Event = "post-release";

    /// <inheritdoc/>
    static string IHookEvent.Context => Context;

    /// <inheritdoc/>
    static string IHookEvent.Event => Event;

    /// <summary>
    /// Gets the description of the version being released.
    /// </summary>
    public required ReleaseInfo Release { get; init; }

    /// <summary>
    /// Gets the packages produced by the release, as a map from package ID to version.
    /// </summary>
    public required IReadOnlyDictionary<string, string> ProducedPackages { get; init; }

    /// <summary>
    /// Gets a value indicating whether the built-in self-reference rewrites will run in this release.
    /// </summary>
    /// <remarks>
    /// <para>The resolved outcome of the <c>release.dogfood</c> option, which the <c>--dogfood</c> command-line
    /// option may have overridden away from the configured value. The rewrites run right after the hook, so this
    /// is what is about to happen, not what has already happened.</para>
    /// </remarks>
    public required bool Dogfooding { get; init; }

    /// <summary>
    /// Loads the args of the current hook run from the hook's args file.
    /// </summary>
    /// <param name="homeDirectory">The home directory the args file path is resolved against;
    /// the current directory when omitted (hooks run from the home directory).</param>
    /// <returns>The deserialized hook args.</returns>
    /// <exception cref="BuildvanaRuntimeException">
    /// The args file does not exist (<c>bv</c> has never run this hook in this repository, or the current
    /// directory is not the home directory), cannot be read, or does not contain valid args.
    /// </exception>
    public static PostReleaseHookArgs Load(string? homeDirectory = null)
        => Load(BuildvanaJsonContext.Default.PostReleaseHookArgs, homeDirectory);
}
