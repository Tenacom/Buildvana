// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.ConsoleOutput;

namespace Buildvana.Tool.Services;

partial class DotNetService
{
    /// <summary>
    /// Represents the output streaming configuration for a <c>dotnet</c> invocation.
    /// </summary>
    private readonly struct OutputStreaming
    {
        /// <summary>
        /// Determines whether output streaming is enabled.
        /// </summary>
        public readonly bool Enabled;

        /// <summary>
        /// The minimum verbosity level at which <c>dotnet</c> output will be streamed.
        /// If this field is <see langword="null"/>, <c>dotnet</c> output will be streamed regardless of the current verbosity level.
        /// </summary>
        /// <remarks>
        /// This field will be ignored by <see cref="RunDotNetAsync"/> if <see cref="Enabled"/> is <see langword="false"/> .
        /// </remarks>
        public readonly Verbosity? Verbosity;

        private OutputStreaming(bool enabled, Verbosity? verbosity)
        {
            Enabled = enabled;
            Verbosity = verbosity;
        }

        /// <summary>
        /// Gets an <see cref="OutputStreaming"/> instance representing disabled output streaming.
        /// </summary>
        public static OutputStreaming Disabled => new(false, null);

        /// <summary>
        /// Gets an <see cref="OutputStreaming"/> instance representing enabled output streaming unrestrained by verbosity.
        /// </summary>
        public static OutputStreaming Unconditional => new(true, null);

        /// <summary>
        /// Gets an <see cref="OutputStreaming"/> instance representing enabled output streaming at the specified verbosity level.
        /// </summary>
        public static OutputStreaming AtVerbosity(Verbosity verbosity) => new(true, verbosity);
    }
}
