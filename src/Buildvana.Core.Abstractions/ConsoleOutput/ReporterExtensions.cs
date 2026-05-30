// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Text;

namespace Buildvana.Core.ConsoleOutput;

/// <summary>
/// Provides extension methods for <see cref="IReporter"/> instances: per-level message shortcuts and
/// <see cref="CompositeFormat"/>-based formatting overloads.
/// </summary>
/// <remarks>
/// All formatting uses <see cref="CultureInfo.InvariantCulture"/>. Cache the <see cref="CompositeFormat"/>
/// instances passed to the formatting overloads in <c>static readonly</c> fields at the call site.
/// </remarks>
#pragma warning disable CA1034 // Nested types should not be visible — false positive on C# 14 extension blocks; fixed in .NET 11, backport to .NET 10 requested in https://github.com/dotnet/sdk/issues/53984
#pragma warning disable CA1708 // Identifiers should differ by more than case — false positive on classes with C# 14 extension blocks; fixed in .NET 11, https://github.com/dotnet/sdk/issues/51716
public static class ReporterExtensions
{
    extension(IReporter @this)
    {
        /// <summary>Reports an <see cref="MessageLevel.Error"/> message.</summary>
        /// <param name="message">The message text.</param>
        public void Error(string message) => @this.Report(MessageLevel.Error, message);

        /// <summary>Reports a <see cref="MessageLevel.Warning"/> message.</summary>
        /// <param name="message">The message text.</param>
        public void Warning(string message) => @this.Report(MessageLevel.Warning, message);

        /// <summary>Reports an <see cref="MessageLevel.Info"/> message.</summary>
        /// <param name="message">The message text.</param>
        public void Info(string message) => @this.Report(MessageLevel.Info, message);

        /// <summary>Reports a <see cref="MessageLevel.Detail"/> message.</summary>
        /// <param name="message">The message text.</param>
        public void Detail(string message) => @this.Report(MessageLevel.Detail, message);

        /// <summary>Reports a <see cref="MessageLevel.Trace"/> message.</summary>
        /// <param name="message">The message text.</param>
        public void Trace(string message) => @this.Report(MessageLevel.Trace, message);

        /// <summary>Formats and reports an <see cref="MessageLevel.Error"/> message.</summary>
        /// <param name="format">The composite format string.</param>
        /// <param name="args">The arguments to format.</param>
        public void Error(CompositeFormat format, params ReadOnlySpan<object?> args)
            => @this.Report(MessageLevel.Error, format, args);

        /// <summary>Formats and reports a <see cref="MessageLevel.Warning"/> message.</summary>
        /// <param name="format">The composite format string.</param>
        /// <param name="args">The arguments to format.</param>
        public void Warning(CompositeFormat format, params ReadOnlySpan<object?> args)
            => @this.Report(MessageLevel.Warning, format, args);

        /// <summary>Formats and reports an <see cref="MessageLevel.Info"/> message.</summary>
        /// <param name="format">The composite format string.</param>
        /// <param name="args">The arguments to format.</param>
        public void Info(CompositeFormat format, params ReadOnlySpan<object?> args)
            => @this.Report(MessageLevel.Info, format, args);

        /// <summary>Formats and reports a <see cref="MessageLevel.Detail"/> message.</summary>
        /// <param name="format">The composite format string.</param>
        /// <param name="args">The arguments to format.</param>
        public void Detail(CompositeFormat format, params ReadOnlySpan<object?> args)
            => @this.Report(MessageLevel.Detail, format, args);

        /// <summary>Formats and reports a <see cref="MessageLevel.Trace"/> message.</summary>
        /// <param name="format">The composite format string.</param>
        /// <param name="args">The arguments to format.</param>
        public void Trace(CompositeFormat format, params ReadOnlySpan<object?> args)
            => @this.Report(MessageLevel.Trace, format, args);

        /// <summary>
        /// Formats and reports a message at the given <paramref name="level"/>. Formatting is skipped entirely
        /// when <paramref name="level"/> is not enabled by the reporter's <see cref="IReporter.Verbosity"/>.
        /// </summary>
        /// <param name="level">The severity of the message.</param>
        /// <param name="format">The composite format string.</param>
        /// <param name="args">The arguments to format.</param>
        public void Report(MessageLevel level, CompositeFormat format, params ReadOnlySpan<object?> args)
        {
            ArgumentNullException.ThrowIfNull(format);
            if (!@this.IsEnabled(level))
            {
                return;
            }

            @this.Report(level, string.Format(CultureInfo.InvariantCulture, format, args));
        }

        /// <summary>
        /// Determines whether a message at the given <paramref name="level"/> would be rendered at the
        /// reporter's current <see cref="IReporter.Verbosity"/>.
        /// </summary>
        /// <param name="level">The level to test.</param>
        /// <returns><see langword="true"/> if the level is enabled; otherwise, <see langword="false"/>.</returns>
        public bool IsEnabled(MessageLevel level) => (int)level <= (int)@this.Verbosity;

        /// <summary>
        /// Determines whether the reporter's <see cref="IReporter.Verbosity"/> is at least the given
        /// <paramref name="minimumVerbosity"/>.
        /// </summary>
        /// <param name="minimumVerbosity">The minimum verbosity to test against.</param>
        /// <returns>
        /// <see langword="true"/> if the reporter's verbosity is at least <paramref name="minimumVerbosity"/>;
        /// otherwise, <see langword="false"/>.
        /// </returns>
        public bool IsVerbosityAtLeast(Verbosity minimumVerbosity) => (int)minimumVerbosity <= (int)@this.Verbosity;
    }
}
