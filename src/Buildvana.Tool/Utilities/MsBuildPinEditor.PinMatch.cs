// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Utilities;

partial class MsBuildPinEditor
{
    // A located pin plus the position of its version value in the file's text; the value's length is the
    // length of the pin's version text.
    private readonly record struct PinMatch(MsBuildPin Pin, int VersionStart);
}
