// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Configuration;

/// <summary>
/// A resolved NuGet push target: source URL and API key.
/// </summary>
/// <param name="Source">The source URL of the NuGet feed.</param>
/// <param name="ApiKey">The API key used to authenticate the push.</param>
internal sealed record NuGetPushTarget(string Source, string ApiKey);
