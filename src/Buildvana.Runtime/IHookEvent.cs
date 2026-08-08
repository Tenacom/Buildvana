// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// Names the hook an args type belongs to: the context and event that select the hook file
/// (see <see cref="WellKnownPaths.GetHookFile"/>) and its args file
/// (see <see cref="WellKnownPaths.GetHookArgsFile"/>).
/// Implemented by every hook's args type, so that a hook's identity travels with its args type
/// and the two can never be mismatched.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
#pragma warning disable CA1716 // Identifiers should not match keywords — 'Event' is reserved in Visual Basic, but hooks are C# file-based apps by construction, so no other-language implementation of this interface can exist
public interface IHookEvent
{
    /// <summary>
    /// Gets the name of the context the hook's event belongs to.
    /// </summary>
    static abstract string Context { get; }

    /// <summary>
    /// Gets the name of the event that triggers the hook.
    /// </summary>
    static abstract string Event { get; }
}
