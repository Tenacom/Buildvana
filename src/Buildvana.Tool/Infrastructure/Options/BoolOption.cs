// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Tool.Infrastructure.Options;

public class BoolOption : Option<bool>
{
    public BoolOption(string commandLineName, string description, params string[] aliases)
        : base(commandLineName, description, aliases)
    {
    }

    public override bool Resolve(BuildContext context)
    {
        if (!HasArgument(context))
        {
            return false;
        }

        var value = GetArgument(context);
        if (value == null)
        {
            return true;
        }

        return !value.Equals(false.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public void AssertTrue(BuildContext context)
    {
        var value = Resolve(context);
        if (!value)
        {
            throw new InvalidOperationException($"{CommandLineName} is not specified");
        }
    }
}
