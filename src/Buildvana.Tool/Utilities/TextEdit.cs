// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Utilities;

/// <summary>
/// A single splice into a text: the <see cref="Length"/> characters starting at <see cref="Start"/> are
/// replaced by <see cref="Text"/>.
/// </summary>
/// <param name="Start">The index of the first replaced character.</param>
/// <param name="Length">The number of replaced characters.</param>
/// <param name="Text">The replacement text.</param>
internal readonly record struct TextEdit(int Start, int Length, string Text);
