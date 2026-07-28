// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;

/// <summary>
/// An <see cref="EngineServices"/> whose importance filtering is controlled by
/// <see cref="MinimumImportance"/>: the least important level still logged, or
/// <see langword="null"/> to log nothing at all.
/// </summary>
internal sealed class StubEngineServices : EngineServices
{
    public MessageImportance? MinimumImportance { get; init; } = MessageImportance.Low;

    public override bool LogsMessagesOfImportance(MessageImportance importance)
        => MinimumImportance is { } minimum && (int)importance <= (int)minimum;
}
