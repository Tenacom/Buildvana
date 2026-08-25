// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Json;

// A keyed-object element in the value-property shape: {"pattern": "policy"}.
[JsonKeyedObject(nameof(Pattern), nameof(Policy))]
internal sealed record KeyedValueSample
{
    public required string Pattern { get; init; }

    public required string Policy { get; init; }
}
