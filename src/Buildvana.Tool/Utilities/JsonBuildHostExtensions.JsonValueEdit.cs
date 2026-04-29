// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Utilities;

partial class JsonBuildHostExtensions
{
    private readonly record struct JsonValueEdit(int Start, int Length, byte[] Replacement);
}
