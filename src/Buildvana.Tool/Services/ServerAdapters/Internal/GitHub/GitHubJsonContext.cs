// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Buildvana.Tool.Services.ServerAdapters.Internal.GitHub;

/// <summary>
/// The source-generated serializer context for the requests and answers of the GitHub REST API.
/// </summary>
/// <remarks>
/// <para>Reading ignores unknown members, because every GitHub answer carries many more members than
/// <c>bv</c> reads. A required member that is missing fails the read.</para>
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    RespectNullableAnnotations = true)]
[JsonSerializable(typeof(GitHubRepository))]
[JsonSerializable(typeof(GitHubRelease))]
[JsonSerializable(typeof(GitHubNewRelease))]
[JsonSerializable(typeof(GitHubReleaseUpdate))]
[JsonSerializable(typeof(GitHubReleaseNotesRequest))]
[JsonSerializable(typeof(GitHubReleaseNotes))]
[JsonSerializable(typeof(GitHubErrorBody))]
internal sealed partial class GitHubJsonContext : JsonSerializerContext;
