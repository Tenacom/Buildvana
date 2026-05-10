// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Tool.Cli;

/// <summary>
/// Declares which kinds of MSBuild switches a <c>bv</c> command accepts and forwards.
/// Consumed by the help renderer to surface the contract on the root help and per-command help;
/// runtime enforcement is planned for a later phase.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class AcceptsMSBuildOptionsAttribute(MSBuildOptionKinds kinds) : Attribute
{
    /// <summary>
    /// Gets the kinds of MSBuild switches the decorated command accepts.
    /// </summary>
    public MSBuildOptionKinds Kinds { get; } = kinds;
}
