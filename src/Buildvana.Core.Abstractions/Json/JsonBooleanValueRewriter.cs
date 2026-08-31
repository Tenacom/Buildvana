// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Buildvana.Core.Json;

/// <summary>
/// Computes a replacement for a JSON boolean value visited during a structural walk of a JSON document.
/// </summary>
/// <param name="propertyPath">
/// The chain of property names from the document root to the current value. The same list instance is
/// reused across invocations and mutated by the walker; do not retain it past the callback's return.
/// Array indices are not included; the callback is only invoked for boolean values that are direct
/// properties of an object.
/// </param>
/// <param name="currentValue">The boolean value currently in the document.</param>
/// <returns>The new value to splice into the document, or <see langword="null"/> to leave it unchanged.</returns>
public delegate bool? JsonBooleanValueRewriter(IReadOnlyList<string> propertyPath, bool currentValue);
