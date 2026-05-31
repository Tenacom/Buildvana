// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Buildvana.Core.JsonSchema;

// A model that exercises every shaping attribute in one schema, for JsonSchemaGeneratorTests.
[JsonSchemaTitle("Sample Title")]
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Reflected over by the schema generator under test; never instantiated.")]
internal sealed record GeneratorSample
{
    // Nullable, no opt-in: the schema should drop "null" from the type.
    public string? Plain { get; init; }

    // Nullable with opt-in: the schema should keep "null".
    [JsonNullable]
    public string? Maybe { get; init; }

    [Description("a described field")]
    public string? Described { get; init; }

    [JsonAllowedKeys("alpha, beta")]
    public IReadOnlyDictionary<string, string>? Map { get; init; }
}
