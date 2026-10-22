// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.Platforms;

/// <summary>
/// A script fake for <see cref="CIPlatform"/>: every member answers with a constant. The platform has no step outputs.
/// </summary>
internal sealed class FakeCIPlatform : CIPlatform
{
    /// <summary>
    /// Gets the value of <see cref="IsCI"/>.
    /// </summary>
    public bool RunsOnCI { get; init; } = true;

    /// <summary>
    /// Gets the value of <see cref="BotIdentity"/>.
    /// </summary>
    public GitIdentity? Identity { get; init; } = new("Buildvana Test Bot", "bot@buildvana.invalid");

    /// <inheritdoc/>
    public override string Name => "Test CI platform";

    /// <inheritdoc/>
    public override bool IsCI => RunsOnCI;

    /// <inheritdoc/>
    public override GitIdentity? BotIdentity => Identity;
}
