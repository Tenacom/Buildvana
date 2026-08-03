// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Buildvana.Runtime;

/// <summary>
/// The source-generated serializer context for all JSON documents exchanged between <c>bv</c>, Buildvana SDK,
/// and repository-owned hooks: the configuration file and hook context files.
/// </summary>
/// <remarks>
/// <para>Being source-generated, the context works in every host, including file-based apps, which run with
/// reflection-based serialization disabled.</para>
/// <para>Reading is strict: comments and trailing commas are allowed, but unknown object members are rejected.
/// A configuration file has already been validated by <c>bv</c> when a hook reads it, and hook context files are
/// written by the same <c>bv</c> version that runs the hook, so a strict read can only fail on files edited by
/// hand after the fact.</para>
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    UseStringEnumConverter = true,
    WriteIndented = true)]
[JsonSerializable(typeof(BuildvanaConfig))]
[JsonSerializable(typeof(PostReleaseHookContext))]
public sealed partial class BuildvanaJsonContext : JsonSerializerContext;
