// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Buildvana.Core.Json.Schema;

// A model exercising every keyed-object schema rendering variation, for JsonSchemaGeneratorTests.
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Reflected over by the schema generator under test; never instantiated.")]
internal sealed record KeyedSchemaSample
{
    // Value shape: additionalProperties is the value property's schema.
    [Description("the policies")]
    public IReadOnlyList<KeyedValueSample>? Policies { get; init; }

    // Remaining-members shape: additionalProperties is the element schema minus the key.
    public IReadOnlyList<KeyedGroupSample>? Groups { get; init; }

    // Remaining-members shape with a second required member, which pruning the key must keep.
    public IReadOnlyList<KeyedRequiredSample>? RequiredGroups { get; init; }

    // A keyed list nested inside a keyed element.
    public IReadOnlyList<KeyedNestedSample>? Nested { get; init; }

    // Nullable with opt-in: the schema should keep "null" beside "object".
    [JsonNullable]
    public IReadOnlyList<KeyedValueSample>? MaybePolicies { get; init; }

    // Dictionary-valued in JSON, so [JsonAllowedKeys] closes its key set.
    [JsonAllowedKeys("first, second")]
    public IReadOnlyList<KeyedValueSample>? LimitedPolicies { get; init; }

    // A key that is not required, so its member names carry no non-blank constraints.
    public IReadOnlyList<KeyedOptionalKeySample>? OptionalKeys { get; init; }

    // Examples on both sides of the element: one describes a member name, the other a member value.
    public IReadOnlyList<ExampleKeyedSample>? Exemplified { get; init; }

    // An example on an optional key, which is reason enough for propertyNames to exist.
    public IReadOnlyList<ExampleOptionalKeySample>? ExemplifiedOptionalKeys { get; init; }
}
