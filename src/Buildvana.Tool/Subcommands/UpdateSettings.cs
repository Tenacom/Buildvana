// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel;
using Buildvana.Tool.CommandLine;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Subcommands;

/// <summary>
/// Options for the <c>update</c> command, parsed from the command's option tokens by <see cref="Parse"/>.
/// Decorated with <see cref="BvOptionAttribute"/>/<see cref="DescriptionAttribute"/> for the help renderer.
/// </summary>
internal sealed class UpdateSettings
{
    /// <summary>
    /// Gets a value indicating whether the update may downgrade pins that are newer than the running bv.
    /// </summary>
    [BvOption("--force")]
    [Description("Update the repository's pins even when they are newer than this bv (downgrade).")]
    public bool Force { get; init; }

    /// <summary>
    /// Parses the command's option tokens into an <see cref="UpdateSettings"/>. Unknown options have already
    /// been rejected by <c>CommandArgumentValidator</c>, so every option token is one the command declares.
    /// </summary>
    /// <param name="options">The option tokens for the <c>update</c> command (from <c>CommandParameters.Options</c>).</param>
    /// <returns>The parsed settings.</returns>
    public static UpdateSettings Parse(IReadOnlyList<string> options)
    {
        Guard.IsNotNull(options);
        var reader = new CliOptionReader(options);
        return new UpdateSettings { Force = reader.ReadFlag("--force") };
    }
}
