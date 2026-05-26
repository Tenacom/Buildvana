// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Buildvana.Core.Process.Internal;

/// <summary>
/// Provides extension methods for <see cref="StringBuilder"/>.
/// </summary>
internal static class StringBuilderExtensions
{
    extension(StringBuilder @this)
    {
        public StringBuilder AppendHeadTail(HeadTailPipeTarget capture, string name)
        {
            if (capture.TotalLines == 0)
            {
                return @this;
            }

            return @this
                .AppendHeader(capture.Truncated
                    ? $"{name} ({capture.TotalLines} total lines, displaying first {capture.HeadLines} and last {capture.TailLines})"
                    : name)
                .AppendLines(capture.Head)
                .AppendHeader(capture.Truncated ? $"... {capture.TruncatedLines} lines truncated ..." : null)
                .AppendLines(capture.Tail);
        }

        public StringBuilder AppendLines(IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                @this = @this.AppendLine(line);
            }

            return @this;
        }

        public StringBuilder AppendHeader(string? header) => header is null ? @this : @this.AppendLine(CultureInfo.InvariantCulture, $"=== {header} ===");
    }
}
