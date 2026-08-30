// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Buildvana.Core.Json.Schema;

// A model whose example fragment is not JSON at all, so generating its schema has to fail.
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Reflected over by the schema generator under test; never instantiated.")]
internal sealed record BadExampleSample
{
    [JsonSchemaExample("not json")]
    public string? Broken { get; init; }
}
