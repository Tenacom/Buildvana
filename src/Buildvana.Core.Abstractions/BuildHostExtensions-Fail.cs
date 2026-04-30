// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
        /// <para>Fails the build with the specified message.</para>
        /// <para>This method does not return.</para>
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="message">A message explaining the reason for failing the build.</param>
        /// <returns>This method never returns.</returns>
        [DoesNotReturn]
        public T Fail<T>(string message)
        {
            @this.Fail(message);
            throw new UnreachableException();
        }

        /// <summary>
        /// <para>Fails the build with the specified formatted message.</para>
        /// <para>This method does not return.</para>
        /// </summary>
        /// <param name="format">A <see cref="CompositeFormat"/>.</param>
        /// <param name="args">An object span that contains zero or more objects to format.</param>
        [DoesNotReturn]
        public void Fail(
            CompositeFormat format,
            params ReadOnlySpan<object?> args)
            => @this.Fail(string.Format(CultureInfo.InvariantCulture, format, args));

        /// <summary>
        /// <para>Fails the build with the specified formatted message.</para>
        /// <para>This method does not return.</para>
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="format">A <see cref="CompositeFormat"/>.</param>
        /// <param name="args">An object span that contains zero or more objects to format.</param>
        [DoesNotReturn]
        public T Fail<T>(
            CompositeFormat format,
            params ReadOnlySpan<object?> args)
            => @this.Fail<T>(string.Format(CultureInfo.InvariantCulture, format, args));
    }
}
