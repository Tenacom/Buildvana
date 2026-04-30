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
        /// <para>Fails the build because an unsupported property setter has been called.</para>
        /// <para>This method does not return.</para>
        /// </summary>
        /// <param name="propertyName">The name of the unsupported property. This parameter defaults to the name of the calling property (or method).</param>
        /// <param name="sourceFilePath">The path of the source file where the unsupported property is accessed. This parameter defaults to the caller's file path.</param>
        /// <param name="sourceLineNumber">The line number in the source file where the unsupported property is accessed. This parameter defaults to the caller's line number.</param>
        [DoesNotReturn]
        public void FailOnUnsupportedProperty([CallerMemberName] string propertyName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
            => @this.Fail($"Unsupported property {propertyName} in {sourceFilePath} ({sourceLineNumber})");

        /// <summary>
        /// <para>Fails the build because an unsupported property getter has been called.</para>
        /// <para>This method does not return.</para>
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="propertyName">The name of the unsupported property. This parameter defaults to the name of the calling property (or method).</param>
        /// <param name="sourceFilePath">The path of the source file where the unsupported property is accessed. This parameter defaults to the caller's file path.</param>
        /// <param name="sourceLineNumber">The line number in the source file where the unsupported property is accessed. This parameter defaults to the caller's line number.</param>
        /// <returns>This method never returns.</returns>
        [DoesNotReturn]
        public T FailOnUnsupportedProperty<T>([CallerMemberName] string propertyName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
            => @this.Fail<T>($"Unsupported property {propertyName} in {sourceFilePath} ({sourceLineNumber})");
    }
}
