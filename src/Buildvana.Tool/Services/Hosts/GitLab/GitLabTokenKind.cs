// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Hosts.GitLab;

/// <summary>
/// Specifies the kind of a GitLab token, which decides the header that carries it.
/// </summary>
internal enum GitLabTokenKind
{
    /// <summary>
    /// A personal, project, or group access token, carried by the <c>PRIVATE-TOKEN</c> header.
    /// </summary>
    AccessToken,

    /// <summary>
    /// The job token of GitLab CI, carried by the <c>JOB-TOKEN</c> header.
    /// </summary>
    JobToken,
}
