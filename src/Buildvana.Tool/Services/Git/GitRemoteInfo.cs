// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Git;

/// <summary>
/// Describes a remote of a Git repository.
/// </summary>
/// <param name="Name">The name of the remote.</param>
/// <param name="Url">The fetch URL of the remote, as the Git configuration states it.</param>
internal sealed record GitRemoteInfo(string Name, string Url);
