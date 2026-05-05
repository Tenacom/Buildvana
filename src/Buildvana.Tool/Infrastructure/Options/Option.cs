// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Buildvana.Tool.Infrastructure.Options;

#pragma warning disable CA1716 // Identifiers should not match keywords - VB.NET keywords are fair game, otherwise half the English dictionary would be disqualified.
public abstract class Option : IOption
#pragma warning restore CA1716 // Identifiers should not match keywords
{
    protected Option(string commandLineName, string description, params string[] aliases)
    {
        CommandLineName = commandLineName;
        Description = description;
        Aliases = aliases;
    }

    public string CommandLineName { get; }

    public string Description { get; }

    public IReadOnlyList<string> Aliases { get; }
}
