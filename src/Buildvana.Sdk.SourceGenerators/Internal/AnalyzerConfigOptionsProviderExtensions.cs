// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Buildvana.Sdk.SourceGenerators.Internal;

/// <summary>
/// Provides extension methods for <c>AnalyzerConfigOptionsProvider</c> instances.
/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible — false positive on C# 14 extension blocks; fixed in .NET 11, backport to .NET 10 requested in https://github.com/dotnet/sdk/issues/53984
#pragma warning disable CA1708 // Identifiers should differ by more than case — false positive on classes with C# 14 extension blocks; fixed in .NET 11, https://github.com/dotnet/sdk/issues/51716
internal static class AnalyzerConfigOptionsProviderExtensions
{
    extension(AnalyzerConfigOptionsProvider @this)
    {
        public bool? GetBooleanMSBuildProperty(string name)
            => @this.GlobalOptions.TryGetValue($"build_property.{name}", out var value) ? value.Equals("true", StringComparison.OrdinalIgnoreCase) : null;

        public string? GetMSBuildProperty(string name)
            => @this.GlobalOptions.TryGetValue($"build_property.{name}", out var value) ? value : null;
    }
}
