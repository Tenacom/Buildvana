// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Buildvana.Core.Configuration;

/// <summary>
/// The source-generated serializer context for reading a Buildvana configuration file into its wire model,
/// <see cref="BuildvanaJsonConfig"/>.
/// </summary>
/// <remarks>
/// <para>Reading is strict: comments and trailing commas are allowed, but unknown object members are rejected.
/// The context has no writing role: configuration files are written by users, never by Buildvana.</para>
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(BuildvanaJsonConfig))]
public sealed partial class BuildvanaJsonConfigContext : JsonSerializerContext;
