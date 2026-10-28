// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Reflection;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.Testing;

/// <summary>
/// Provides extension methods for <see cref="Assembly"/> instances.
/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible — false positive on C# 14 extension blocks; fixed in .NET 11, backport to .NET 10 requested in https://github.com/dotnet/sdk/issues/53984
#pragma warning disable CA1708 // Identifiers should differ by more than case — false positive on classes with C# 14 extension blocks; fixed in .NET 11, https://github.com/dotnet/sdk/issues/51716
public static class AssemblyExtensions
{
    extension(Assembly @this)
    {
        /// <summary>
        /// Gets the value of an assembly metadata item, such as one a test project supplies with an
        /// <c>AssemblyMetadata</c> item.
        /// </summary>
        /// <param name="key">The key of the metadata item.</param>
        /// <returns>The value of the first metadata item with the given key.</returns>
        /// <exception cref="InvalidOperationException">The assembly has no metadata item with the given key, or the
        /// item has no value.</exception>
        public string GetMetadata(string key)
        {
            Guard.IsNotNull(key);
            var value = @this.GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))
                ?.Value;
            return value ?? throw new InvalidOperationException($"{@this.GetName().Name} has no metadata item \"{key}\" with a value.");
        }
    }
}
