// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Buildvana.Core.Testing;

/// <summary>
/// An invocation of <see cref="FakeProcessRunner.RunWithInheritedStdioAsync"/> captured by a <see cref="FakeProcessRunner"/>.
/// </summary>
/// <param name="Executable">The executable that was run.</param>
/// <param name="Args">The arguments passed to <paramref name="Executable"/>.</param>
/// <param name="Environment">The environment overrides applied to the child, or <see langword="null"/> for none.</param>
/// <param name="WorkingDirectory">The working directory of the child, or <see langword="null"/> for the inherited one.</param>
public sealed record InheritedStdioRun(
    string Executable,
    IReadOnlyList<string> Args,
    IReadOnlyDictionary<string, string?>? Environment,
    string? WorkingDirectory);
