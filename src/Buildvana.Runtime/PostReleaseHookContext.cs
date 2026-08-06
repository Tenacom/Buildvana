// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// The context handed to the <c>release/post-release</c> hook: serialized by <c>bv</c> to the hook's context
/// file before the hook runs, deserialized by the hook via <see cref="Load"/>.
/// </summary>
/// <remarks>
/// <para>The contract is additive-only: newer <c>bv</c> versions may add properties, never remove or repurpose
/// existing ones. Properties added after the initial release must be optional with a default value, so that a
/// context file written before an update can still be loaded.</para>
/// </remarks>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record PostReleaseHookContext
{
    /// <summary>The name of the command this hook belongs to.</summary>
    public const string Command = "release";

    /// <summary>The name of the moment this hook fires at.</summary>
    public const string Moment = "post-release";

    /// <summary>
    /// Gets run-time information about the bv run: the running version, how the run was launched, and the
    /// absolute paths of the run's well-known directories.
    /// </summary>
    public required RuntimeInfo RuntimeInfo { get; init; }

    /// <summary>
    /// Gets the description of the version being released.
    /// </summary>
    public required ReleaseInfo Release { get; init; }

    /// <summary>
    /// Gets the packages produced by the release, as a map from package ID to version.
    /// </summary>
    public required IReadOnlyDictionary<string, string> ProducedPackages { get; init; }

    /// <summary>
    /// Gets a value indicating whether the built-in self-reference rewrites ran in this release.
    /// </summary>
    public required bool Dogfooded { get; init; }

    /// <summary>
    /// Loads the context of the current hook run from the hook's context file.
    /// </summary>
    /// <param name="homeDirectory">The home directory the context file path is resolved against;
    /// the current directory when omitted (hooks run from the home directory).</param>
    /// <returns>The deserialized hook context.</returns>
    /// <exception cref="BuildvanaRuntimeException">
    /// The context file does not exist (<c>bv</c> has never run this hook in this repository, or the current
    /// directory is not the home directory), cannot be read, or does not contain a valid context.
    /// </exception>
    public static PostReleaseHookContext Load(string? homeDirectory = null)
    {
        var relativePath = WellKnownPaths.GetHookContextFile(Command, Moment);
        var path = string.IsNullOrEmpty(homeDirectory) ? relativePath : Path.Combine(homeDirectory, relativePath);
        if (!File.Exists(path))
        {
            throw new BuildvanaRuntimeException(
                $"The hook context file '{path}' does not exist. "
                + "Run this program from the home directory of a repository where bv has run this hook at least once.");
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception e) when (e.IsIORelatedException)
        {
            throw new BuildvanaRuntimeException($"Could not read from {path}: {e.Message}", e);
        }

        try
        {
            return JsonSerializer.Deserialize(json, BuildvanaJsonContext.Default.PostReleaseHookContext)
                ?? throw new BuildvanaRuntimeException($"The hook context file '{path}' does not contain a JSON object.");
        }
        catch (JsonException e)
        {
            throw new BuildvanaRuntimeException($"Invalid hook context file {path}: {e.Message}", e);
        }
    }
}
