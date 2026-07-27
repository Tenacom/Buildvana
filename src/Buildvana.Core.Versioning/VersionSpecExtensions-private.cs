// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace Buildvana.Core.Versioning;

partial class VersionSpecExtensions
{
    private static readonly Regex VersionSpecRegex = GetVersionSpecRegex();

    // Semver grammar minus patch and build metadata, with a single loosened (ASCII-alphanumeric) tag whose
    // text is not captured: only the presence of the dash matters. The \s*$ tail tolerates trailing
    // whitespace (e.g. the version file's trailing newline) but rejects trailing junk.
    [GeneratedRegex(
        @"^(?<Major>0|[1-9][0-9]*)\.(?<Minor>0|[1-9][0-9]*)((?<Prerelease>-)[0-9a-zA-Z]*)?\s*$",
        RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant)]
    private static partial Regex GetVersionSpecRegex();
}
