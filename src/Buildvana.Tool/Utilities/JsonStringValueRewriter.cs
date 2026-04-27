// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Buildvana.Tool.Utilities;

/// <summary>
/// Computes a replacement for a JSON string value visited during a structural walk of a JSON document.
/// </summary>
/// <param name="propertyPath">
/// The chain of property names from the document root to the current value. The same list instance is
/// reused across invocations and mutated by the walker; do not retain it past the callback's return.
/// Array indices are not included; the callback is only invoked for string values that are direct
/// properties of an object.
/// </param>
/// <param name="currentValue">The string value currently in the document, fully unescaped.</param>
/// <returns>The new value to splice into the document, or <see langword="null"/> to leave it unchanged.</returns>
public delegate string? JsonStringValueRewriter(IReadOnlyList<string> propertyPath, string currentValue);
