// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Dependencies;

// Diagnostic codes reported while managing dependencies. Documented in docs/ToolDiagnostics.md
// (dependency management, BV1200-BV1299).
internal static class DiagnosticCodes
{
    public const string UnknownPackage = "BV1200";
    public const string UnknownVersion = "BV1201";
    public const string UnknownNetSdkVersion = "BV1202";
    public const string NoSuchPin = "BV1203";
}
