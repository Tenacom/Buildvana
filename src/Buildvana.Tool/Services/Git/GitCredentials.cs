// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Git;

/// <summary>
/// Represents the identity of a Git user.
/// </summary>
/// <param name="Username">The username of the Git user.</param>
/// <param name="Password">The password of the Git user.</param>
public record GitCredentials(string Username, string Password);
