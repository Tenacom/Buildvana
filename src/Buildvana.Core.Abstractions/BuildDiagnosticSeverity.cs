// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core;

/// <summary>
/// The severity of a <see cref="BuildDiagnostic"/>.
/// </summary>
public enum BuildDiagnosticSeverity
{
    /// <summary>A problem that does not by itself prevent the build from succeeding.</summary>
    Warning,

    /// <summary>A problem that prevents the build from succeeding.</summary>
    Error,
}
