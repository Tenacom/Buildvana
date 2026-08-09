// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

namespace Buildvana.Core.ConsoleOutput;

public abstract partial class TextWriterReporter
{
    private sealed class ActivityScope : IActivityScope
    {
        private readonly TextWriterReporter _reporter;
        private readonly long _startTimestamp;
        private bool _completed;
        private bool _disposed;

        public ActivityScope(TextWriterReporter reporter, string title, int depth)
        {
            _reporter = reporter;
            Title = title;
            Depth = depth;
            _startTimestamp = Stopwatch.GetTimestamp();
        }

        public string Title { get; }

        public int Depth { get; }

        public TimeSpan Elapsed => Stopwatch.GetElapsedTime(_startTimestamp);

        public string? OutcomeMessage { get; private set; }

        public void Complete(string? outcomeMessage = null)
        {
            _completed = true;
            OutcomeMessage = outcomeMessage;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _reporter.EndActivity(this, _completed);
        }
    }
}
