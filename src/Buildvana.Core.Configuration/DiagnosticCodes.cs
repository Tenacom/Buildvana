// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.Configuration;

// Diagnostic codes reported while loading a configuration file. Documented in docs/ToolDiagnostics.md
// (JSON schema validation, BV1100-BV1199).
internal static class DiagnosticCodes
{
    public const string InvalidJson = "BV1100";
    public const string TypeMismatch = "BV1101";
    public const string DisallowedValue = "BV1102";
    public const string UnknownProperty = "BV1103";
    public const string MissingProperty = "BV1104";
    public const string ValueNotAllowed = "BV1105";
}
