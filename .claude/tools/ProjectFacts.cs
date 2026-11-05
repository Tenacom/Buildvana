// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

/*
 * The facts about this repository that the tools of this directory read. `Common.props` compiles this file into
 * every tool, so a tool holds no fact of its own, and a copy of `.claude` rewrites this file alone. The index of
 * `.claude/CLAUDE.md` lists it among the project-specific files.
 */

using System.Collections.Generic;

/// <summary>
/// The facts about this repository that the tools read. A fact about the repository as a whole sits here, and the
/// facts one tool reads sit in a nested class named after what the tool checks.
/// </summary>
internal static class ProjectFacts
{
    /// <summary>
    /// The platform of the Git host that renders the documentation: "GitHub", "GitLab", or "Forgejo".
    /// It selects the anchor a heading gets, as "Git hosts" in <c>.claude/rules/common/documentation.md</c> says.
    /// <c>.claude/rules/project/repository.md</c> names the host of this repository.
    /// </summary>
    public const string GitHost = "GitHub";

    /// <summary>
    /// The facts <c>lint-docs.cs</c> reads.
    /// </summary>
    /// <remarks>
    /// A glob is matched against the path of a file relative to the repository root, with forward slashes. In a
    /// glob, <c>*</c> matches any run of characters within a path segment, <c>?</c> matches one such character, and
    /// <c>**</c> matches any run of characters, slashes included. <c>**/README.md</c> matches a README.md at the
    /// root and in any folder.
    /// </remarks>
    internal static class Documentation
    {
        /// <summary>
        /// The generated folders, as paths relative to the root with forward slashes.
        /// <c>.claude/rules/project/documentation.md</c> lists them, with the tool that owns each. The tool skips
        /// them. None here.
        /// </summary>
        public static IReadOnlyList<string> GeneratedFolders { get; } = [];

        /// <summary>
        /// Globs of the files the TODO check leaves alone. None here.
        /// </summary>
        public static IReadOnlyList<string> TodoExemptFiles { get; } = [];

        /// <summary>
        /// Globs of the files the sentence check leaves alone, beyond the non-technical documentation that "What is
        /// documentation" in <c>.claude/rules/common/documentation.md</c> lists, which the tool knows. None here.
        /// </summary>
        public static IReadOnlyList<string> SentenceExemptFiles { get; } = [];
    }

    /// <summary>
    /// The facts <c>lint-commit.cs</c> reads.
    /// </summary>
    internal static class CommitMessages
    {
        /// <summary>
        /// Words the tool rejects at the start of a subject, beyond the list the tool holds. None here.
        /// </summary>
        public static IReadOnlyList<string> AnnouncingVerbs { get; } = [];

        /// <summary>
        /// Words and phrases the tool rejects in a commit message, beyond the list the tool holds. None here.
        /// </summary>
        public static IReadOnlyList<string> BannedWords { get; } = [];
    }
}
