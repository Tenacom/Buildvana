// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Runtime;

/// <summary>
/// Provides the well-known paths that make up the contract between <c>bv</c> and repository-owned hooks.
/// All paths are relative to the home directory and use forward slashes.
/// </summary>
public static class WellKnownPaths
{
    /// <summary>
    /// The path of bv's scratch directory, holding machine-generated temporary files.
    /// Repositories are expected to gitignore it; <c>bv</c> never considers its contents when detecting
    /// working-tree changes, whether or not they do, and <c>bv clean</c> deletes it.
    /// </summary>
    public const string ScratchDirectory = ".buildvana-temp";

    /// <summary>
    /// The path of the directory holding hook context files, one per hook, at
    /// <c>{command}/{moment}.json</c> paths mirroring the <c>.buildvana/hooks/{command}/{moment}.cs</c>
    /// convention for hook files.
    /// </summary>
    public const string HookContextsDirectory = ScratchDirectory + "/hook-contexts";

    /// <summary>
    /// Gets the path of the context file for the hook of a command and moment.
    /// The file is (re)written by <c>bv</c> before each run of that hook and left in place afterwards,
    /// so a hook can be re-run by hand against the context of the last run.
    /// </summary>
    /// <param name="command">The name of the command the hook belongs to.</param>
    /// <param name="moment">The name of the moment the hook fires at.</param>
    /// <returns>The path of the hook's context file, relative to the home directory.</returns>
    public static string GetHookContextFile(string command, string moment)
        => $"{HookContextsDirectory}/{command}/{moment}.json";
}
