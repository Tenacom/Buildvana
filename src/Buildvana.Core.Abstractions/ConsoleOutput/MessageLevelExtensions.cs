// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Core.ConsoleOutput;

/// <summary>
/// Provides extension methods for <see cref="MessageLevel"/> values.
/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible — false positive on C# 14 extension blocks; fixed in .NET 11, backport to .NET 10 requested in https://github.com/dotnet/sdk/issues/53984
#pragma warning disable CA1708 // Identifiers should differ by more than case — false positive on classes with C# 14 extension blocks; fixed in .NET 11, https://github.com/dotnet/sdk/issues/51716
public static class MessageLevelExtensions
{
    extension(MessageLevel @this)
    {
        /// <summary>
        /// Gets the least verbose <see cref="Verbosity"/> at which a message of this level is shown.
        /// </summary>
        /// <returns>The minimum <see cref="Verbosity"/> that enables this level.</returns>
        /// <exception cref="ArgumentOutOfRangeException">This level is not a known <see cref="MessageLevel"/>.</exception>
        /// <remarks>
        /// <para>This method is the single authority on when a level becomes visible. Every
        /// <see cref="IReporter"/> implementation answers the question through it — directly, or by mapping the
        /// answer onto the visibility rules of whatever it renders through — so that a given level becomes
        /// visible at the same point no matter which reporter is in play.</para>
        /// <para>The mapping cannot be a comparison of the two enums' underlying values: there are more levels
        /// than there are thresholds, so no ordering of the members makes such a comparison give the right
        /// answer for all of them.</para>
        /// </remarks>
        public Verbosity MinimumVerbosity()
        {
            return @this switch
            {
                MessageLevel.Error => Verbosity.Quiet,
                MessageLevel.Warning or MessageLevel.Notice => Verbosity.Minimal,
                MessageLevel.Info => Verbosity.Normal,
                MessageLevel.Detail => Verbosity.Detailed,
                MessageLevel.Trace => Verbosity.Diagnostic,
                _ => ThrowUnknownLevel(@this),
            };

            // The exception names the offending value "level", as every caller of this method does. The name
            // cannot come from the receiver: nameof(@this) yields "this", which names the parameter the
            // compiler emits rather than anything a caller can see.
            static Verbosity ThrowUnknownLevel(MessageLevel level)
                => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown message level.");
        }
    }
}
