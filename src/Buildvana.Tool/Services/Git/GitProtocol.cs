// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Git;

/// <summary>
/// Represents the protocol used by a Git URL.
/// </summary>
public enum GitProtocol
{
    /// <summary>
    /// HTTP protocol.
    /// </summary>
    Http,

    /// <summary>
    /// HTTPS protocol.
    /// </summary>
    Https,

    /// <summary>
    /// SSH protocol.
    /// </summary>
    Ssh,

    /// <summary>
    /// Git protocol.
    /// </summary>
    Git,
}
