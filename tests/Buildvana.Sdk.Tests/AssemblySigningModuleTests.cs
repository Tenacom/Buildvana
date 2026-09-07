// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Buildvana.Core.Testing;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

// Runs the real targets of the AssemblySigning module against a temporary project, and reads
// AssemblyOriginatorKeyFile back after the build. The project stubs ResolveKeySource, the .NET SDK target
// that reads the property, and declares the task itself, loaded in process. The conversion target used to
// run before Build, that is after the DependsOnTargets of Build, where ResolveKeySource had already
// refused the .pfx file.
// One MSBuild build manager serves the whole process, so target tests run one at a time.
[NotInParallel]
internal sealed class AssemblySigningModuleTests
{
    private const string Password = "p@ss";
    private const string TempSnkFileName = "BuildvanaTemp.snk";

    [Test]
    public async Task ResolveKeySource_WithPfxKeyFile_ReadsTheConvertedKeyFile()
    {
        var result = Run("key.pfx", signAssembly: true);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(Path.GetFileName(result.KeyFile)).IsEqualTo(TempSnkFileName);
        await Assert.That(result.TempSnkExists).IsTrue();
    }

    [Test]
    public async Task ResolveKeySource_WithUpperCasePfxExtension_ReadsTheConvertedKeyFile()
    {
        var result = Run("KEY.PFX", signAssembly: true);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(Path.GetFileName(result.KeyFile)).IsEqualTo(TempSnkFileName);
        await Assert.That(result.TempSnkExists).IsTrue();
    }

    [Test]
    public async Task ResolveKeySource_WithSignAssemblyFalse_ReadsThePfxUnchanged()
    {
        var result = Run("key.pfx", signAssembly: false);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(Path.GetFileName(result.KeyFile)).IsEqualTo("key.pfx");
        await Assert.That(result.TempSnkExists).IsFalse();
    }

    private static (bool Succeeded, string KeyFile, bool TempSnkExists) Run(string pfxFileName, bool signAssembly)
    {
        using var home = new TempHome();
        var pfxPath = home.GetFullPath(pfxFileName);
        WritePfx(pfxPath);

        // PrepareForBuild creates the intermediate output directory in a real build.
        var objDirectory = home.GetFullPath("obj") + Path.DirectorySeparatorChar;
        _ = Directory.CreateDirectory(objDirectory);

        var tasksAssembly = Path.Combine(AppContext.BaseDirectory, "Buildvana.Sdk.Tasks.dll");
        var projectPath = home.GetFullPath("Test.proj");
        var projectText = $"""
            <Project>
              <PropertyGroup>
                <SignAssembly>{(signAssembly ? "true" : "false")}</SignAssembly>
                <AssemblyOriginatorKeyFile>{pfxPath}</AssemblyOriginatorKeyFile>
                <AssemblyOriginatorKeyPassword>{Password}</AssemblyOriginatorKeyPassword>
                <IntermediateOutputPath>{objDirectory}</IntermediateOutputPath>
              </PropertyGroup>
              <UsingTask TaskName="ConvertPfxToSnk" AssemblyFile="{tasksAssembly}" />
              <Import Project="{GetRealTargetsPath()}" />
              <Target Name="ResolveKeySource" />
            </Project>
            """;
        home.WriteFile("Test.proj", projectText);

        var globalProperties = new Dictionary<string, string?>();
        using var collection = new ProjectCollection(globalProperties);
        var logger = new RecordingMSBuildLogger();
        var parameters = new BuildParameters(collection) { Loggers = [logger] };
        var request = new BuildRequestData(
            projectPath,
            globalProperties,
            null,
            ["ResolveKeySource"],
            null,
            BuildRequestDataFlags.ProvideProjectStateAfterBuild);
        var result = BuildManager.DefaultBuildManager.Build(parameters, request);
        var keyFile = result.ProjectStateAfterBuild?.GetPropertyValue("AssemblyOriginatorKeyFile") ?? string.Empty;
        var tempSnkExists = File.Exists(Path.Combine(objDirectory, TempSnkFileName));
        return (result.OverallResult == BuildResultCode.Success, keyFile, tempSnkExists);
    }

    private static void WritePfx(string path)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Buildvana test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, Password));
    }

    private static string GetRealTargetsPath()
        => typeof(AssemblySigningModuleTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(static attribute => attribute.Key == "RealAssemblySigningModuleTargetsPath")
            .Value!;
}
