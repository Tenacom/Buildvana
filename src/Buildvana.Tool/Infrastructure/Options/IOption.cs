// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Buildvana.Tool.Infrastructure.Options;

public interface IOption
{
    string CommandLineName { get; }

    string Description { get; }

    IReadOnlyList<string> Aliases { get; }
}
