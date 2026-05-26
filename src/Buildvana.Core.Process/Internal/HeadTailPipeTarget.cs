// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;

namespace Buildvana.Core.Process.Internal;

/// <summary>
/// A <see cref="PipeTarget"/> that retains the first <see cref="MaxHeadLines"/> and last <see cref="MaxTailLines"/> lines
/// of a stream, discarding the lines in between.
/// </summary>
internal sealed class HeadTailPipeTarget : PipeTarget
{
    private readonly List<string> _head = [];
    private readonly Queue<string> _tail = [];
    private long _totalLines;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeadTailPipeTarget"/> class.
    /// </summary>
    /// <param name="maxHeadLines">The maximum number of leading lines to retain.</param>
    /// <param name="maxTailLines">The maximum number of trailing lines to retain.</param>
    public HeadTailPipeTarget(int maxHeadLines, int maxTailLines)
    {
        MaxHeadLines = maxHeadLines;
        MaxTailLines = maxTailLines;
    }

    /// <summary>
    /// Gets the maximum number of leading lines retained from the stream.
    /// </summary>
    public int MaxHeadLines { get; }

    /// <summary>
    /// Gets the maximum number of trailing lines retained from the stream.
    /// </summary>
    public int MaxTailLines { get; }

    /// <summary>
    /// Gets the leading lines retained from the stream.
    /// </summary>
    public IEnumerable<string> Head => _head;

    /// <summary>
    /// Gets the trailing lines retained from the stream.
    /// </summary>
    public IEnumerable<string> Tail => _tail;

    /// <summary>
    /// Gets the total number of lines read from the stream.
    /// </summary>
    public long TotalLines => _totalLines;

    /// <summary>
    /// Gets the number of leading lines retained from the stream.
    /// </summary>
    public long HeadLines => _head.Count;

    /// <summary>
    /// Gets the number of trailing lines retained from the stream.
    /// </summary>
    public long TailLines => _tail.Count;

    /// <summary>
    /// Gets the number of retained lines, i.e. the lines that <see cref="AppendTo"/> outputs, excluding the omission marker.
    /// </summary>
    public long DisplayedLines => _head.Count + _tail.Count;

    /// <summary>
    /// Gets a value indicating whether any lines were omitted, i.e. whether <see cref="TotalLines"/> exceeded
    /// the sum of <see cref="MaxHeadLines"/> and <see cref="MaxTailLines"/>.
    /// </summary>
    public bool Truncated => _totalLines > MaxHeadLines + MaxTailLines;

    /// <summary>
    /// Gets the number of lines omitted between the retained head and tail, or <c>0</c> if no line was omitted.
    /// </summary>
    public long TruncatedLines => Truncated ? _totalLines - _head.Count - _tail.Count : 0;

    /// <inheritdoc/>
    public override async Task CopyFromAsync(Stream origin, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(origin);
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            _totalLines++;
            if (_head.Count < MaxHeadLines)
            {
                _head.Add(line);
            }
            else
            {
                _tail.Enqueue(line);
                if (_tail.Count > MaxTailLines)
                {
                    _tail.Dequeue();
                }
            }
        }
    }

    /// <summary>
    /// Appends the retained head lines, an omission marker if <see cref="Truncated"/> is <see langword="true"/>,
    /// and the retained tail lines to the specified <see cref="StringBuilder"/>.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append to.</param>
    /// <returns>The same <see cref="StringBuilder"/> instance passed as <paramref name="sb"/>, to allow call chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sb"/> is <see langword="null"/>.</exception>
    public StringBuilder AppendTo(StringBuilder sb)
    {
        ArgumentNullException.ThrowIfNull(sb);

        foreach (var l in _head)
        {
            sb = sb.AppendLine(l);
        }

        if (Truncated)
        {
            sb = sb.AppendLine(CultureInfo.InvariantCulture, $"... [{TruncatedLines} line(s) omitted] ...");
        }

        foreach (var l in _tail)
        {
            sb = sb.AppendLine(l);
        }

        return sb;
    }
}
