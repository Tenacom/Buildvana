// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using CommunityToolkit.Diagnostics;
using Microsoft.CodeAnalysis.Options;

namespace Buildvana.Tool.Infrastructure.Options;

public sealed class Example
{
    private readonly List<ExampleArgument> _arguments = [];

    public Example(string taskName)
    {
        TaskName = taskName;
    }

    public string TaskName { get; }

    public IReadOnlyCollection<ExampleArgument> Arguments => _arguments;

    public Example WithMsBuildArgument(string name, string value)
    {
        Guard.IsNotNull(name);
        Guard.IsNotNull(value);

        _arguments.Add(new ExampleArgument(name, value, true));
        return this;
    }

    public Example WithArgument(BoolOption option)
    {
        Guard.IsNotNull(option);

        _arguments.Add(new ExampleArgument(option.CommandLineName, null, false));
        return this;
    }

    public Example WithArgument(StringOption option, string value)
    {
        Guard.IsNotNull(option);
        Guard.IsNotNull(value);

        _arguments.Add(new ExampleArgument(option.CommandLineName, value, false));
        return this;
    }
}
