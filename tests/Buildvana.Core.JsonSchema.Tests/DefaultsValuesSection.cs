// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

// The nested defaults section paired with DefaultsSchemaSection.
internal sealed record DefaultsValuesSection
{
    public string Inner { get; init; } = "nested-default";
}
