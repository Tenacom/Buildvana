// Copyright (C) Tenacom and contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Buildvana.Sdk.SourceGenerators.Internal;
using Microsoft.CodeAnalysis;

namespace Buildvana.Sdk.SourceGenerators;

/// <summary>
/// Generates the <c>CLSCompliant</c> and <c>ComVisible</c> assembly attributes
/// from the MSBuild properties of the same names. C# projects only.
/// </summary>
[Generator(LanguageNames.CSharp)]
public class AdditionalAssemblyInfoGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var attributeValuesProvider = context
                                     .AnalyzerConfigOptionsProvider
                                     .Select(static (p, _)
                                          => new AdditionalAssemblyInfoValues(
                                              p.GetBooleanMSBuildProperty("CLSCompliant"),
                                              p.GetBooleanMSBuildProperty("ComVisible")));

        var enableNullableProvider = context.ParseOptionsProvider
                                            .Select(static (options, _) => options.SupportsNullableReferenceTypes);

        context.RegisterSourceOutput(attributeValuesProvider.Combine(enableNullableProvider), static (spc, data) => {
            var (values, enableNullable) = data;
            var sb = new StringBuilder()
                    .AppendFileHeader(enableNullable)
                    .AppendBooleanAssemblyAttribute("global::System.CLSCompliant", values.CLSCompliant)
                    .AppendBooleanAssemblyAttribute("global::System.Runtime.InteropServices.ComVisible", values.ComVisible);

            spc.AddSource("BV_AssemblyAttributes.g.cs", sb.ToString());
        });
    }
}
