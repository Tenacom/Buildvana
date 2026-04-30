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
        /// Logs a formatted message at the specified <see cref="LogLevel"/>.
        /// </summary>
        /// <param name="level">The <see cref="LogLevel"/> at which to log the message.</param>
        /// <param name="format">A <see cref="CompositeFormat"/>.</param>
        /// <param name="args">An object span that contains zero or more objects to format.</param>
        public void Log(
            LogLevel level,
            CompositeFormat format,
            params ReadOnlySpan<object?> args)
            => @this.Log(level, string.Format(CultureInfo.InvariantCulture, format, args));
    }
}
