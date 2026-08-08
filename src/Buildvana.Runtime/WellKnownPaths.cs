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
    /// The path of the directory holding hook files, one per hook, at <c>{context}/{event}.cs</c>
    /// paths (see <see cref="GetHookFile"/>).
    /// </summary>
    public const string HooksDirectory = ".buildvana/hooks";

    /// <summary>
    /// The path of the directory holding hook args files, one per hook, at
    /// <c>{context}/{event}.json</c> paths mirroring the hook file convention under
    /// <see cref="HooksDirectory"/>.
    /// </summary>
    public const string HookArgsDirectory = ScratchDirectory + "/hook-args";

    /// <summary>
    /// Gets the path of the hook file for a context and event.
    /// This is the file <c>bv</c> looks for when the event occurs: the hook runs if the file
    /// exists, and is skipped otherwise.
    /// </summary>
    /// <param name="context">The name of the context the hook's event belongs to.</param>
    /// <param name="event">The name of the event that triggers the hook.</param>
    /// <returns>The path of the hook file, relative to the home directory.</returns>
    public static string GetHookFile(string context, string @event)
        => $"{HooksDirectory}/{context}/{@event}.cs";

    /// <summary>
    /// Gets the path of the args file for the hook of a context and event.
    /// The file is (re)written by <c>bv</c> before each run of that hook and left in place afterwards,
    /// so a hook can be re-run by hand against the args of the last run.
    /// </summary>
    /// <param name="context">The name of the context the hook's event belongs to.</param>
    /// <param name="event">The name of the event that triggers the hook.</param>
    /// <returns>The path of the hook's args file, relative to the home directory.</returns>
    public static string GetHookArgsFile(string context, string @event)
        => $"{HookArgsDirectory}/{context}/{@event}.json";
}
