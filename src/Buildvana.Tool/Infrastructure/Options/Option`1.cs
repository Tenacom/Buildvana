// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Cake.Common;
using Cake.Core;

namespace Buildvana.Tool.Infrastructure.Options;

#pragma warning disable CA1716 // Identifiers should not match keywords - VB.NET keywords are fair game, otherwise half the English dictionary would be disqualified.
public abstract class Option<T> : IOption
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

    private IEnumerable<string> AllNames
    {
        get
        {
            yield return CommandLineName;
            foreach (var alias in Aliases)
            {
                yield return alias;
            }
        }
    }

    private IEnumerable<string> AllStrippedNames => AllNames.Select(name => name.TrimStart('-'));

    public abstract T Resolve(BuildContext context);

    protected bool HasArgument(BuildContext context) => AllStrippedNames.Any(context.HasArgument);

    protected string? GetArgument(BuildContext context) => AllStrippedNames
        .Where(context.HasArgument)
        .Select(name => context.Arguments.GetArgument(name))
        .FirstOrDefault();
}
