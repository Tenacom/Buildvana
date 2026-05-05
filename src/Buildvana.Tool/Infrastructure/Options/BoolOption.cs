// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Infrastructure.Options;

public sealed class BoolOption : Option
{
    public BoolOption(string commandLineName, string description, params string[] aliases)
        : base(commandLineName, description, aliases)
    {
    }
}
