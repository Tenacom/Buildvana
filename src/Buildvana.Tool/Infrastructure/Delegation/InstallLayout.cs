// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Infrastructure.Delegation;

/// <summary>
/// The kind of installation the running bv executes from, inferred from its base directory by
/// <see cref="InstallLayoutDetector"/>. The layouts are not contractual SDK surface, so detection degrades to
/// <see cref="Unknown"/> for anything unrecognized; see each member for how the delegation decision treats it.
/// </summary>
internal enum InstallLayout
{
    /// <summary>
    /// bv executes from an extracted NuGet package (<c>…/bv/&lt;version&gt;/tools/&lt;tfm&gt;/&lt;rid&gt;/</c>),
    /// the way manifest-run (<c>dotnet bv</c>) and <c>dnx</c>-run tools do. Treated as local: when its version
    /// matches the repository's tool manifest pin, it runs in place — a manifest-run bv always matches the pin
    /// by construction, and delegating here would add a pointless process hop to identical bits on every
    /// <c>dotnet bv</c> invocation.
    /// </summary>
    PackageCache,

    /// <summary>
    /// bv executes from a tool store (<c>…/.store/bv/&lt;version&gt;/…</c>), the way global
    /// (<c>dotnet tool install -g</c>) and <c>--tool-path</c> installs do. Confidently non-local: it always
    /// delegates to the version pinned in the repository's tool manifest.
    /// </summary>
    ToolStore,

    /// <summary>
    /// The base directory matches no known layout. Treated as non-local — when in doubt, the repository's
    /// pinned version wins; a mis-detected local bv wastes one process hop at worst, since the child never
    /// re-delegates (it matches the manifest pin, and the recursion marker backstops even that).
    /// </summary>
    Unknown,
}
