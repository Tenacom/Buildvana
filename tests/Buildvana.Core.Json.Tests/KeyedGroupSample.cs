// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Json;

// A keyed-object element in the remaining-members shape: {"caption": { ... }}.
[JsonKeyedObject(nameof(Caption))]
internal sealed record KeyedGroupSample
{
    public required string Caption { get; init; }

    public string? Files { get; init; }

    public int Retries { get; init; }

    public IReadOnlyList<string>? Tags { get; init; }
}
