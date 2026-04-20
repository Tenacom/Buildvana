// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Infrastructure.Options;

public sealed record ExampleArgument(string Name, string? Value, bool IsMsBuild);
