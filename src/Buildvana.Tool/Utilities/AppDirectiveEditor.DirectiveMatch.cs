// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Utilities;

partial class AppDirectiveEditor
{
    // A located directive plus the position of its version text in the file's text; negative when the
    // directive has no version. The version's length is the length of the directive's version text.
    private readonly record struct DirectiveMatch(AppDirective Directive, int VersionStart);
}
