// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Infrastructure;

/// <summary>
/// A no-op <see cref="ICommandOptions"/> that always reports no value.
/// Stand-in until bv has its own command-line argument parser.
/// </summary>
#pragma warning disable CA1812 // Avoid uninstantiated internal classes - Instantiated via DI in BuildContext.
internal sealed class EmptyCommandOptions : ICommandOptions
#pragma warning restore CA1812
{
    public string? GetValue(string name) => null;
}
