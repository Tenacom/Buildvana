// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Buildvana.Sdk.Tasks;
using Microsoft.Build.Framework;

// Runs ConvertPfxToSnk on certificates the test writes itself. The output is read back with
// RSACryptoServiceProvider.ImportCspBlob, a reader of the key blob layout that exists on every platform.
internal sealed class ConvertPfxToSnkTests
{
    private const string Password = "p@ss";

    [Test]
    public async Task Execute_WritesThePrivateKeyOfThePfx()
    {
        await RunInTempDirectory(async (engine, directory) =>
        {
            var (pfxPath, key) = WriteRsaPfx(directory, Password);
            var outputPath = Path.Combine(directory, "key.snk");
            await Assert.That(CreateTask(engine, pfxPath, Password, outputPath).Execute()).IsTrue();
            await AssertKeyFile(outputPath, key).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    [Test]
    public async Task Execute_WithNoPassword_WritesThePrivateKeyOfThePfx()
    {
        await RunInTempDirectory(async (engine, directory) =>
        {
            var (pfxPath, key) = WriteRsaPfx(directory, null);
            var outputPath = Path.Combine(directory, "key.snk");
            await Assert.That(CreateTask(engine, pfxPath, string.Empty, outputPath).Execute()).IsTrue();
            await AssertKeyFile(outputPath, key).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    [Test]
    public async Task Execute_WithWrongPassword_LogsBVSDK1201()
    {
        await RunInTempDirectory(async (engine, directory) =>
        {
            var (pfxPath, _) = WriteRsaPfx(directory, Password);
            var outputPath = Path.Combine(directory, "key.snk");
            await Assert.That(CreateTask(engine, pfxPath, "wrong", outputPath).Execute()).IsFalse();
            await Assert.That(engine.Errors.Count).IsEqualTo(1);
            await Assert.That(engine.Errors[0].Message!).Contains("BVSDK1201");
        }).ConfigureAwait(false);
    }

    [Test]
    public async Task Execute_WithEcdsaKey_LogsBVSDK1202()
    {
        await RunInTempDirectory(async (engine, directory) =>
        {
            var pfxPath = WriteEcdsaPfx(directory);
            var outputPath = Path.Combine(directory, "key.snk");
            await Assert.That(CreateTask(engine, pfxPath, Password, outputPath).Execute()).IsFalse();
            await Assert.That(engine.Errors.Count).IsEqualTo(1);
            await Assert.That(engine.Errors[0].Message!).Contains("BVSDK1202");
        }).ConfigureAwait(false);
    }

    [Test]
    public async Task Execute_WithMissingPfxPath_LogsBVSDK1050()
    {
        var engine = new RecordingBuildEngine();
        await Assert.That(CreateTask(engine, string.Empty, Password, "key.snk").Execute()).IsFalse();
        await Assert.That(engine.Errors.Count).IsEqualTo(1);
        await Assert.That(engine.Errors[0].Message!).Contains("BVSDK1050");
    }

    private static ConvertPfxToSnk CreateTask(IBuildEngine engine, string pfxPath, string password, string outputPath)
        => new()
        {
            BuildEngine = engine,
            PfxPath = pfxPath,
            PfxPassword = password,
            OutputPath = outputPath,
        };

    private static (string PfxPath, RSAParameters Key) WriteRsaPfx(string directory, string? password)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Buildvana test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return (WritePfx(directory, request, password), rsa.ExportParameters(includePrivateParameters: true));
    }

    private static string WriteEcdsaPfx(string directory)
    {
        using var ecdsa = ECDsa.Create();
        var request = new CertificateRequest("CN=Buildvana test", ecdsa, HashAlgorithmName.SHA256);
        return WritePfx(directory, request, Password);
    }

    private static string WritePfx(string directory, CertificateRequest request, string? password)
    {
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var path = Path.Combine(directory, "test.pfx");
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, password));
        return path;
    }

    private static async Task AssertKeyFile(string path, RSAParameters expected)
    {
        var blob = await File.ReadAllBytesAsync(path).ConfigureAwait(false);

        // The algorithm id of the header is CALG_RSA_SIGN, as in a key file written by sn -k.
        await Assert.That(BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(4))).IsEqualTo(0x2400u);

        using var rsa = new RSACryptoServiceProvider();
        rsa.ImportCspBlob(blob);
        var actual = rsa.ExportParameters(includePrivateParameters: true);
        await Assert.That(Trim(actual.Modulus)).IsEquivalentTo(Trim(expected.Modulus));
        await Assert.That(Trim(actual.Exponent)).IsEquivalentTo(Trim(expected.Exponent));
        await Assert.That(Trim(actual.D)).IsEquivalentTo(Trim(expected.D));
        await Assert.That(Trim(actual.P)).IsEquivalentTo(Trim(expected.P));
        await Assert.That(Trim(actual.Q)).IsEquivalentTo(Trim(expected.Q));
        await Assert.That(Trim(actual.DP)).IsEquivalentTo(Trim(expected.DP));
        await Assert.That(Trim(actual.DQ)).IsEquivalentTo(Trim(expected.DQ));
        await Assert.That(Trim(actual.InverseQ)).IsEquivalentTo(Trim(expected.InverseQ));
    }

    // Exporters differ in the leading zeros they keep, so values compare without them.
    private static byte[] Trim(byte[]? value) => [.. (value ?? []).SkipWhile(static b => b == 0)];

    private static async Task RunInTempDirectory(Func<RecordingBuildEngine, string, Task> test)
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        try
        {
            await test(new RecordingBuildEngine(), tempDirectory.FullName).ConfigureAwait(false);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }
}
