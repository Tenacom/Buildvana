// Copyright (C) Tenacom and contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// Creates the C# compilations a generator test runs against,
/// and checks the compilation the generator run leaves behind.
/// </summary>
internal static class TestCompilation
{
    public const string AssemblyName = "TestAssembly";

    private static readonly CSharpCompilationOptions Options = new(OutputKind.DynamicallyLinkedLibrary);

    public static CSharpCompilation Create()
        => Create(GetReferences());

    public static CSharpCompilation Create(IEnumerable<MetadataReference> references)
        => CSharpCompilation.Create(AssemblyName, references: references, options: Options);

    public static MetadataReference CreateReference(string assemblyName, string source)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            GetReferences(),
            Options);

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
        {
            var errors = string.Join(Environment.NewLine, result.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));
            throw new InvalidOperationException($"The source of {assemblyName} does not compile:{Environment.NewLine}{errors}");
        }

        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    public static async Task AssertHasNoErrors(Compilation compilation)
    {
        var errors = compilation.GetDiagnostics()
                                .Where(static d => d.Severity == DiagnosticSeverity.Error)
                                .ToArray();
        await Assert.That(errors).IsEmpty();
    }

    private static IEnumerable<MetadataReference> GetReferences()
    {
        var trustedAssemblyList = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        Debug.Assert(trustedAssemblyList is not null, "The .NET host lists the trusted platform assemblies of every process.");
        var trustedAssemblies = trustedAssemblyList.Split(Path.PathSeparator);
        return trustedAssemblies
              .Where(static p => Path.GetFileName(p) is "System.Runtime.dll" or "System.Private.CoreLib.dll" or "netstandard.dll")
              .Select(MetadataReference (p) => MetadataReference.CreateFromFile(p));
    }
}
