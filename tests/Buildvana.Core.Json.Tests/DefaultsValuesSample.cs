// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

// The defaults instance for the defaults-emission tests: domain-model style, with every default a
// property initializer. Deliberately a different type from DefaultsSchemaSample — the generator matches
// members by JSON name only.
internal sealed record DefaultsValuesSample
{
    public string Text { get; init; } = "hello";

    public bool Flag { get; init; } = true;

    public SampleLevel Level { get; init; } = SampleLevel.Two;

    public string? NotStated { get; init; }

    public string Dynamic { get; init; } = "computed";

    public IReadOnlyList<string> Tags { get; init; } = ["a"];

    public IReadOnlyList<KeyedValueSample> Policies { get; init; } = [new() { Pattern = "*", Policy = "latest" }];

    public DefaultsValuesSection Section { get; init; } = new();
}
