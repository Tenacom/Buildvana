// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using JetBrains.Annotations;

namespace Buildvana.Tool.Services.Hooks;

/// <summary>
/// The context handed to the <c>release/post-release</c> hook, serialized to a JSON file whose absolute path
/// is published to the hook in the <c>BV_HOOK_CONTEXT</c> environment variable.
/// </summary>
/// <remarks>
/// <para>Serialized property names are camelCase (e.g. <c>releaseSemVer</c>); dictionary keys are serialized
/// verbatim.</para>
/// <para>The serialized form is read by loader code shipped with Buildvana SDK, whose version may transiently
/// differ from the running bv's. The contract's evolution must therefore be additive-only: new properties may
/// be added, but existing ones must never be removed or repurposed.</para>
/// </remarks>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
internal sealed record PostReleaseHookContext
{
    /// <summary>
    /// Gets the absolute path of the home directory. This is also the working directory of the hook.
    /// </summary>
    public required string HomeDirectory { get; init; }

    /// <summary>
    /// Gets the version being released, in simple <c>MAJOR.MINOR.PATCH</c> form, without any prerelease tag.
    /// </summary>
    public required string ReleaseVersion { get; init; }

    /// <summary>
    /// Gets the version being released, in full semantic version form: <see cref="ReleaseVersion"/>, plus
    /// <c>-tag</c> when the version is a prerelease. This is the form used by release tags and embedded in
    /// artifact names.
    /// </summary>
    public required string ReleaseSemVer { get; init; }

    /// <summary>
    /// Gets the previously released version (the latest release tag reachable from <c>HEAD</c>),
    /// or <see langword="null"/> when no previous release exists.
    /// </summary>
    public required string? PreviousVersion { get; init; }

    /// <summary>
    /// Gets a value indicating whether the version being released is a prerelease.
    /// </summary>
    public required bool IsPrerelease { get; init; }

    /// <summary>
    /// Gets a value indicating whether the release is a public release.
    /// </summary>
    public required bool IsPublicRelease { get; init; }

    /// <summary>
    /// Gets the absolute path of the directory containing the build artifacts.
    /// </summary>
    public required string ArtifactsDirectory { get; init; }

    /// <summary>
    /// Gets the packages produced by the release, as a map from package ID to version.
    /// </summary>
    public required IReadOnlyDictionary<string, string> ProducedPackages { get; init; }

    /// <summary>
    /// Gets a value indicating whether the built-in self-reference rewrites ran in this release.
    /// </summary>
    public required bool Dogfooded { get; init; }
}
