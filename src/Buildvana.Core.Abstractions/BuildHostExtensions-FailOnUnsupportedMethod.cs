// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Buildvana.Core;

#pragma warning disable CA1034 // Nested types should not be visible — false positive on C# 14 extension blocks; fixed in .NET 11, backport to .NET 10 requested in https://github.com/dotnet/sdk/issues/53984
#pragma warning disable CA1708 // Identifiers should differ by more than case — false positive on classes with C# 14 extension blocks; fixed in .NET 11, https://github.com/dotnet/sdk/issues/51716
partial class BuildHostExtensions
{
    extension(IBuildHost @this)
    {
        /// <summary>
        /// <para>Fails the build because an unsupported method has been called.</para>
        /// <para>This method does not return.</para>
        /// </summary>
        /// <param name="methodName">The name of the unsupported method. This parameter defaults to the name of the calling method.</param>
        /// <param name="sourceFilePath">The path of the source file where the unsupported method is called. This parameter defaults to the caller's file path.</param>
        /// <param name="sourceLineNumber">The line number in the source file where the unsupported method is called. This parameter defaults to the caller's line number.</param>
        [DoesNotReturn]
        public void FailOnUnsupportedMethod([CallerMemberName] string methodName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
            => @this.Fail($"Unsupported method {methodName} in {sourceFilePath} ({sourceLineNumber})");

        /// <summary>
        /// <para>Fails the build because an unsupported method has been called.</para>
        /// <para>This method does not return.</para>
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="methodName">The name of the unsupported method. This parameter defaults to the name of the calling method.</param>
        /// <param name="sourceFilePath">The path of the source file where the unsupported method is called. This parameter defaults to the caller's file path.</param>
        /// <param name="sourceLineNumber">The line number in the source file where the unsupported method is called. This parameter defaults to the caller's line number.</param>
        /// <returns>This method never returns.</returns>
        [DoesNotReturn]
        public T FailOnUnsupportedMethod<T>([CallerMemberName] string methodName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
            => @this.Fail<T>($"Unsupported method {methodName} in {sourceFilePath} ({sourceLineNumber})");
    }
}
