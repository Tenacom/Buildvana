// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Utilities;

partial class MsBuildPinEditor
{
    // The span of a Version child element's text within the file. The scan's resume position travels
    // separately, as an out parameter, because it is meaningful whether or not a child was found.
    private readonly record struct VersionChild(int Start, int Length);
}
