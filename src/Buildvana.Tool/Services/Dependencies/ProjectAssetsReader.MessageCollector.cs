// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Common;

namespace Buildvana.Tool.Services.Dependencies;

internal static partial class ProjectAssetsReader
{
    // Keeps what NuGet's reader has to say about a file it could not parse. The reader reports the reason
    // through a logger and answers with an empty lock file, so without one the failure has no explanation.
    private sealed class MessageCollector : LoggerBase
    {
        private readonly List<string> _messages = [];

        // The messages as a sentence to append to bv's own, empty when NuGet said nothing.
        public string Summary => _messages.Count == 0 ? string.Empty : " " + string.Join(" ", _messages);

        public override void Log(ILogMessage message) => _messages.Add(message.Message);

        public override Task LogAsync(ILogMessage message)
        {
            Log(message);
            return Task.CompletedTask;
        }
    }
}
