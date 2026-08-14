// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Runtime;

/// <summary>
/// Provides extension methods for `VersioningConfig` instances.
/// </summary>
/// <remarks>
/// <para>The receiver is nullable because a configuration file may omit the <c>versioning</c> section entirely:
/// an absent section and an absent setting mean the same thing, so both resolve to the default here rather
/// than at each call site.</para>
/// </remarks>
#pragma warning disable CA1034 // Nested types should not be visible — false positive on C# 14 extension blocks; fixed in .NET 11, backport to .NET 10 requested in https://github.com/dotnet/sdk/issues/53984
#pragma warning disable CA1708 // Identifiers should differ by more than case — false positive on classes with C# 14 extension blocks; fixed in .NET 11, https://github.com/dotnet/sdk/issues/51716
public static class VersioningConfigExtensions
{
    extension(VersioningConfig? @this)
    {
        /// <summary>
        /// Gets how much of the computed version goes into the assembly version:
        /// <see cref="VersioningConfig.AssemblyVersionPrecision"/>, or
        /// <see cref="VersioningConfig.DefaultAssemblyVersionPrecision"/> when the configuration file
        /// does not set it.
        /// </summary>
        /// <remarks>
        /// <para>The default belongs to the model rather than to any one consumer, so that <c>bv</c>, SDK tasks,
        /// and hooks answer this question identically.</para>
        /// </remarks>
        public AssemblyVersionPrecision EffectiveAssemblyVersionPrecision
            => @this?.AssemblyVersionPrecision ?? VersioningConfig.DefaultAssemblyVersionPrecision;
    }
}
