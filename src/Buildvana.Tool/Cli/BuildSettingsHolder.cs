// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Cli;

/// <summary>
/// Singleton holder for the current command's <see cref="BuildSettings"/>.
/// </summary>
/// <remarks>
/// <para>The active command's <c>ExecuteAsync</c> populates <see cref="Current"/> before any service that
/// reads build options is resolved.</para>
/// </remarks>
public sealed class BuildSettingsHolder
{
    public BuildSettings Current { get; set; } = new();
}
