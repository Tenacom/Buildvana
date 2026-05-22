// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Tool.Cli;

/// <summary>
/// Marks a <c>bv</c> command whose entire post-pre-parse argument list is forwarded verbatim to the
/// underlying <c>dotnet</c> invocation(s) it performs.
/// </summary>
/// <remarks>
/// <para>The pre-parser in <c>Program.Main</c> hands Spectre only the command name and stashes the remaining
/// arguments in <see cref="ForwardedArguments"/> for the command body; <see cref="BvHelpProvider"/> uses the
/// same marker to note the forwarding behavior in help output.</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class ConsumeAllArgumentsAttribute : Attribute;
