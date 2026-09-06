// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// Creates the empty C# compilation a generator test runs against,
/// and checks the compilation the generator run leaves behind.
/// </summary>
internal static class TestCompilation
{
    public static CSharpCompilation Create()
        => CSharpCompilation.Create(
            "TestAssembly",
            references: GetReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    public static async Task AssertHasNoErrors(Compilation compilation)
    {
        var errors = compilation.GetDiagnostics()
                                .Where(static d => d.Severity == DiagnosticSeverity.Error)
                                .ToArray();
        await Assert.That(errors).IsEmpty();
    }

    private static IEnumerable<MetadataReference> GetReferences()
    {
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        return trustedAssemblies
              .Where(static p => Path.GetFileName(p) is "System.Runtime.dll" or "System.Private.CoreLib.dll" or "netstandard.dll")
              .Select(MetadataReference (p) => MetadataReference.CreateFromFile(p));
    }
}
