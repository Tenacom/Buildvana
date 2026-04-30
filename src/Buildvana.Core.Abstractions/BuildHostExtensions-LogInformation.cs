// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Text;

namespace Buildvana.Core;

#pragma warning disable CA1034 // Nested types should not be visible — false positive on C# 14 extension blocks; fixed in .NET 11, backport to .NET 10 requested in https://github.com/dotnet/sdk/issues/53984
#pragma warning disable CA1708 // Identifiers should differ by more than case — false positive on classes with C# 14 extension blocks; fixed in .NET 11, https://github.com/dotnet/sdk/issues/51716
partial class BuildHostExtensions
{
    extension(IBuildHost @this)
    {
        /// <summary>
        /// Logs a message at <see cref="LogLevel.Information"/> level.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void LogInformation(string message) => @this.Log(LogLevel.Information, message);

        /// <summary>
        /// Logs a formatted message at <see cref="LogLevel.Information"/> level.
        /// </summary>
        /// <param name="format">A <see cref="CompositeFormat"/>.</param>
        /// <param name="args">An object span that contains zero or more objects to format.</param>
        public void LogInformation(
            CompositeFormat format,
            params ReadOnlySpan<object?> args)
            => @this.Log(LogLevel.Information, string.Format(CultureInfo.InvariantCulture, format, args));
    }
}
