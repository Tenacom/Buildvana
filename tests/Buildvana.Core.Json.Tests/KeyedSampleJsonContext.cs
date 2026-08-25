// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

// Proves the converter works against source-generated metadata. The options attributes matter: tests copy
// Default.Options (the read-only obstacle to adding a converter directly), and the copy must carry them.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(IReadOnlyList<KeyedGroupSample>))]
internal sealed partial class KeyedSampleJsonContext : JsonSerializerContext
{
}
