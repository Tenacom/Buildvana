// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.ConsoleOutput;

/// <summary>
/// The severity of a message reported through an <see cref="IReporter"/>. The level both classifies the
/// message and, together with the reporter's <see cref="Verbosity"/>, decides whether it is shown.
/// </summary>
/// <remarks>
/// <para>Members are ordered from highest to lowest severity. There are more levels than there are
/// <see cref="Verbosity"/> thresholds, so the two do not map one-to-one: the verbosity from which each level
/// becomes visible is stated by <see cref="MessageLevelExtensions.MinimumVerbosity"/>, which is the single
/// authority on the matter and the one every <see cref="IReporter"/> implementation agrees on.</para>
/// </remarks>
public enum MessageLevel
{
    /// <summary>An error: something went wrong. Shown at every verbosity.</summary>
    Error,

    /// <summary>
    /// A warning: something looks off but is not fatal. Shown at <see cref="Verbosity.Minimal"/> and above.
    /// </summary>
    Warning,

    /// <summary>
    /// A record of a fact: something changed, something was decided, something was deliberately skipped.
    /// Shown at <see cref="Verbosity.Minimal"/> and above.
    /// </summary>
    /// <remarks>
    /// <para>Use this level for what the reader would want to know afterwards — the version spec changed, N files
    /// were rewritten, a step was skipped and why — and <see cref="Info"/> for narration of what the tool is doing
    /// right now. The two are not a loudness ranking: this is a quieter <see cref="Warning"/>, not a louder
    /// <see cref="Info"/>, and it should feel like it costs something. A message at this level survives the default
    /// verbosity, so promoting narration to it makes the default as noisy as <see cref="Verbosity.Normal"/> and
    /// leaves the ladder with no rung meaning what this one means.</para>
    /// </remarks>
    Notice,

    /// <summary>
    /// Narration of what the tool is doing right now. Shown at <see cref="Verbosity.Normal"/> and above.
    /// </summary>
    /// <remarks>
    /// <para>See <see cref="Notice"/> for the criterion that separates the two levels.</para>
    /// </remarks>
    Info,

    /// <summary>A detail useful when following along closely. Shown at <see cref="Verbosity.Detailed"/> and above.</summary>
    Detail,

    /// <summary>Fine-grained diagnostic chatter. Shown only at <see cref="Verbosity.Diagnostic"/>.</summary>
    Trace,
}
