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

    public static readonly BoolOption Help = new(
        "--help",
        "Prints help information",
        "-h");
}
