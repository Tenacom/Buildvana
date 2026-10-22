// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Services.Git;

namespace Buildvana.Tool.Services.Platforms;

/// <summary>
/// A machine with no CI runner, such as a developer machine.
/// </summary>
internal sealed class LocalPlatform : CIPlatform
{
    /// <inheritdoc/>
    public override string Name => "Local";

    /// <inheritdoc/>
    /// <value>Always <see langword="false"/>.</value>
    public override bool IsCI => false;

    /// <inheritdoc/>
    /// <value>Always <see langword="null"/>: a local run commits as the user.</value>
    public override GitIdentity? BotIdentity => null;
}
