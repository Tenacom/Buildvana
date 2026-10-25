// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Buildvana.Tool.Services.Hosts.Forgejo;

/// <summary>
/// The source-generated serializer context for the requests and answers of the Forgejo REST API.
/// </summary>
/// <remarks>
/// <para>Reading ignores unknown members, because every Forgejo answer carries many more members than
/// <c>bv</c> reads. A required member that is missing fails the read.</para>
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    RespectNullableAnnotations = true)]
[JsonSerializable(typeof(ForgejoRepository))]
[JsonSerializable(typeof(ForgejoRelease))]
[JsonSerializable(typeof(ForgejoNewRelease))]
[JsonSerializable(typeof(ForgejoReleaseUpdate))]
[JsonSerializable(typeof(ForgejoErrorBody))]
internal sealed partial class ForgejoJsonContext : JsonSerializerContext;
