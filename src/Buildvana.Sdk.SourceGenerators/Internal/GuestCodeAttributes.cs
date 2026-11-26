// Copyright (C) Tenacom and contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Buildvana.Sdk.SourceGenerators.Internal;

/// <summary>
/// Records which of the attributes of guest code a compilation can name.
/// </summary>
/// <param name="DebuggerNonUserCode"><see langword="true"/> if the compilation can name <c>DebuggerNonUserCodeAttribute</c>;
/// otherwise, <see langword="false"/>.</param>
/// <param name="ExcludeFromCodeCoverage"><see langword="true"/> if the compilation can name <c>ExcludeFromCodeCoverageAttribute</c>;
/// otherwise, <see langword="false"/>.</param>
/// <param name="GeneratedCode"><see langword="true"/> if the compilation can name <c>GeneratedCodeAttribute</c>;
/// otherwise, <see langword="false"/>.</param>
internal readonly record struct GuestCodeAttributes(
    bool DebuggerNonUserCode,
    bool ExcludeFromCodeCoverage,
    bool GeneratedCode)
{
    public static GuestCodeAttributes FromCompilation(Compilation compilation)
        => new(
            CanName(compilation, "System.Diagnostics.DebuggerNonUserCodeAttribute"),
            CanName(compilation, "System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute"),
            CanName(compilation, "System.CodeDom.Compiler.GeneratedCodeAttribute"));

    // GetTypeByMetadataName checks accessibility for well-known types alone,
    // so it returns an internal polyfill that a reference defines.
    private static bool CanName(Compilation compilation, string metadataName)
        => compilation.GetTypeByMetadataName(metadataName) is { } type
        && compilation.IsSymbolAccessibleWithin(type, compilation.Assembly);
}
