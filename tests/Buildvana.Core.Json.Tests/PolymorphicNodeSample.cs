// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

// The base of a polymorphic hierarchy: the exporter renders it as an "anyOf" of the derived types.
[JsonDerivedType(typeof(PolymorphicBranchSample), "branch")]
internal abstract record PolymorphicNodeSample;
