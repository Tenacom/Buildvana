// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Tool.Infrastructure.Options;

public class StringOption : Option<string>
{
    public StringOption(string commandLineName, string description, params string[] aliases)
        : base(commandLineName, description, aliases)
    {
    }

    public override string Resolve(BuildContext context)
    {
        if (!HasArgument(context))
        {
            return string.Empty;
        }

        var value = GetArgument(context);
        if (value == null || string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim();
    }

    public string AssertHasValue(BuildContext context)
    {
        var value = Resolve(context);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{CommandLineName} is not specified");
        }

        return value;
    }
}
