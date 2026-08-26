// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Utilities;

partial class MsBuildPinEditor
{
    // The parts of a parsed start tag that the scanner uses. VersionStart is negative when the tag carries
    // no Version attribute; End is the index just past the tag's closing '>'.
    private readonly record struct StartTag(
        string Name,
        string? IncludeValue,
        int VersionStart,
        int VersionLength,
        bool SelfClosing,
        int End);
}
