// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.IO;

/// <summary>
/// Specifies how file name matching treats letter case.
/// </summary>
public enum CaseSensitivityMode
{
    /// <summary>
    /// Match case-insensitively on operating systems whose file systems are case-insensitive by default
    /// (Windows and macOS); match case-sensitively elsewhere.
    /// </summary>
    SystemDefault,

    /// <summary>
    /// Match case-sensitively.
    /// </summary>
    CaseSensitive,

    /// <summary>
    /// Match case-insensitively.
    /// </summary>
    CaseInsensitive,
}
