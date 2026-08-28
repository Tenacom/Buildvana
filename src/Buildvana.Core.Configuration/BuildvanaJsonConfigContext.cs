// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using Buildvana.Core.Json;

namespace Buildvana.Core.Configuration;

/// <summary>
/// The source-generated serializer context for reading a Buildvana configuration file into its wire model,
/// <see cref="BuildvanaJsonConfig"/>.
/// </summary>
/// <remarks>
/// <para>Reading is strict: comments and trailing commas are allowed, but unknown object members are rejected.
/// The context has no writing role: configuration files are written by users, never by Buildvana.</para>
/// </remarks>
// The Converters property takes a Type[], and an array attribute argument is not CLS-compliant. The attribute
// is compile-time input to the System.Text.Json source generator and reaches no consumer, so the rule that
// CS3016 enforces has nothing to protect here.
#pragma warning disable CS3016 // Arrays as attribute arguments is not CLS-compliant
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    UseStringEnumConverter = true,
    Converters = [typeof(JsonKeyedObjectConverter)])]
#pragma warning restore CS3016
[JsonSerializable(typeof(BuildvanaJsonConfig))]
public sealed partial class BuildvanaJsonConfigContext : JsonSerializerContext;
