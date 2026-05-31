// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.JsonSchema;

partial class JsonSourceMap
{
    // Tracks one open container while walking the document, so a value's pointer can be built from its parent.
    private sealed class Frame(string pointer, bool isArray)
    {
        public string Pointer { get; } = pointer;

        public bool IsArray { get; } = isArray;

        public int NextIndex { get; set; }

        public string? PendingKey { get; set; }
    }
}
