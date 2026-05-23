// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Tool.Cli;

/// <summary>
/// A discovered <c>bv</c> command: the name it is registered under, the class that implements it, and whether
/// it forwards all of its arguments verbatim. Produced by <see cref="CommandRegistry"/> from
/// <see cref="ImplementsCommandAttribute"/>.
/// </summary>
/// <param name="Name">The command name as typed on the command line.</param>
/// <param name="CommandType">The class implementing the command.</param>
/// <param name="ConsumesAllArguments">Whether the command forwards all of its arguments verbatim.</param>
internal sealed record CommandRegistration(string Name, Type CommandType, bool ConsumesAllArguments);
