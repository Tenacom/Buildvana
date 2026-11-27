// Copyright (C) Tenacom and contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Buildvana.Sdk.SourceGenerators.Internal;

/// <summary>
/// Provides extension members for <see cref="ParseOptions"/>.
/// </summary>
internal static class ParseOptionsExtensions
{
    extension(ParseOptions @this)
    {
        // Below C# 8, the compiler rejects a #nullable directive and a nullable annotation with CS8370.
        public bool SupportsNullableReferenceTypes
            => @this is CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp8 };
    }
}
