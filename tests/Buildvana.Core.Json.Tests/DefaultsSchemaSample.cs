// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Buildvana.Core.Json.Schema;

// The type the schema is generated FROM in the defaults-emission tests: all-nullable, wire-model style.
// Its defaults deliberately live on a different type (DefaultsValuesSample), so the tests prove that
// matching is by JSON name, not by type identity.
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Reflected over by the schema generator under test; never instantiated.")]
internal sealed record DefaultsSchemaSample
{
    public string? Text { get; init; }

    public bool? Flag { get; init; }

    public SampleLevel? Level { get; init; }

    // Its counterpart on the defaults instance is null: the schema must state no default.
    public string? NotStated { get; init; }

    // Its counterpart on the defaults instance has a value, but the effective default is dynamic: the
    // attribute must keep it out of the schema.
    [JsonSchemaNoDefault]
    public string? Dynamic { get; init; }

    // Collections carry no default.
    public IReadOnlyList<string>? Tags { get; init; }

    // Object-shaped in the schema, but still a collection: no default, and no recursion into it.
    public IReadOnlyList<KeyedValueSample>? Policies { get; init; }

    public DefaultsSchemaSection? Section { get; init; }
}
