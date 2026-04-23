// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Infrastructure.Options;

public static class KnownOptions
{
    public static readonly StringOption Verbosity = new(
        "--verbosity",
        "Specifies the amount of information to be displayed (Quiet, Minimal, Normal, Verbose, Diagnostic)",
        "-v");

    public static readonly BoolOption Exclusive = new(
        "--exclusive",
        "Executes the target task without any dependencies",
        "-e");

    public static readonly BoolOption DocsPreview = new(
        "--preview",
        "When specified, documentation changelog includes the upcoming version",
        "-p");

    public static readonly StringOption DocsDepth = new(
        "--depth",
#pragma warning disable SA1118 // A parameter must not span multiple lines - Raw string literals are multiline by definition
        """
        The number of last stable versions that requires changelog regenerations.
        Use "all" for all values. The default is zero.
        """,
#pragma warning restore
        "-d");

    public static readonly BoolOption ForceClone = new(
        "--force-clone",
        "Forces re-cloning of the changelog repository, deleting any existing directory.",
        "-f");

    public static readonly BoolOption Help = new(
        "--help",
        "Prints help information",
        "-h");

    public static readonly BoolOption Stable = new(
        "--stable",
        "Removes VersionSuffix in MSBuild settings",
        "-s");

    public static readonly StringOption NextVersion = new(
        "--next-version",
        "Specifies next version number",
        "-n");

    public static readonly BoolOption Push = new(
        "--push",
        "When specified, the task actually pushes to GitHub and nuget.org");
}
