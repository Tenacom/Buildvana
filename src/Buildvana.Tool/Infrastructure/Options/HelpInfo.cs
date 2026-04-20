// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Buildvana.Tool.Infrastructure.Options;

public class HelpInfo
{
    public string Description { get; init; } = string.Empty;

    public IReadOnlyList<IOption> Options { get; init; } = [];

    public IReadOnlyList<EnvVar> EnvironmentVariables { get; init; } = [];

    public IReadOnlyList<Example> Examples { get; init; } = [];
}
