// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Buildvana.Tool.Services.Hosts.GitLab;

/// <summary>
/// The source-generated serializer context for the requests and answers of the GitLab REST API.
/// </summary>
/// <remarks>
/// <para>Reading ignores unknown members, because every GitLab answer carries many more members than
/// <c>bv</c> reads. A required member that is missing fails the read.</para>
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    RespectNullableAnnotations = true)]
[JsonSerializable(typeof(GitLabProject))]
[JsonSerializable(typeof(IReadOnlyList<GitLabRelease>))]
[JsonSerializable(typeof(GitLabRelease))]
[JsonSerializable(typeof(GitLabNewRelease))]
[JsonSerializable(typeof(GitLabPackageFile))]
internal sealed partial class GitLabJsonContext : JsonSerializerContext;
