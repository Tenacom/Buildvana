// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Buildvana.Core;
using Buildvana.Core.Diagnostics;
using Buildvana.Sdk.Internal;
using Buildvana.Sdk.Resources;
using Microsoft.Build.Framework;

namespace Buildvana.Sdk.Tasks;

public sealed class ConvertPfxToSnk : BuildvanaSdkTask
{
    [Required]
    public string PfxPath { get; set; } = string.Empty;

    [Required]
    public string PfxPassword { get; set; } = string.Empty;

    [Required]
    public string OutputPath { get; set; } = string.Empty;

    protected override Undefined Run()
    {
        BuildFailedException.ThrowIf(
            string.IsNullOrEmpty(PfxPath),
            string.Format(CultureInfo.InvariantCulture, Strings.MissingParameterFmt, nameof(PfxPath)));
        BuildFailedException.ThrowIf(
            string.IsNullOrEmpty(OutputPath),
            string.Format(CultureInfo.InvariantCulture, Strings.MissingParameterFmt, nameof(OutputPath)));

        using var cert = LoadCertificate(PfxPath, PfxPassword);
        var keyBytes = ExtractPrivateKey(cert, PfxPath);
        SaveBytes(OutputPath, keyBytes);
        return Undefined.Value;
    }

    private static X509Certificate2 LoadCertificate(string path, string password)
    {
        // Null and empty string are one and the same, as far as task parameters are concerned.
        // https://learn.microsoft.com/en-us/visualstudio/msbuild/task-writing?view=vs-2022#how-msbuild-invokes-a-task
        // X509Certificate2 accepts a null password as "no password", which is (probably) different from an empty password.
        var pwd = password.Length == 0 ? null : password;
        try
        {
            return X509CertificateLoader.LoadPkcs12FromFile(path, pwd, X509KeyStorageFlags.Exportable);
        }
        catch (CryptographicException)
        {
            throw new BuildFailedException(
                string.Format(CultureInfo.InvariantCulture, Strings.AssemblySigning.CannotExtractCertificateFmt, path));
        }
    }

    private static byte[] ExtractPrivateKey(X509Certificate2 certificate, string certificatePath)
    {
        // GetRSAPrivateKey returns RSACng on Windows and RSAOpenSsl on Linux, whatever provider the file names,
        // so the blob is written from the key parameters instead of exported from a CryptoAPI key.
        using var privateKey = certificate.GetRSAPrivateKey() ?? throw MissingRsaPrivateKey(certificatePath);
        try
        {
            return StrongNameKeyBlob.Write(privateKey.ExportParameters(includePrivateParameters: true));
        }
        catch (CryptographicException)
        {
            throw MissingRsaPrivateKey(certificatePath);
        }
    }

    private static BuildFailedException MissingRsaPrivateKey(string certificatePath)
        => new(string.Format(CultureInfo.InvariantCulture, Strings.AssemblySigning.MissingRsaPrivateKeyFmt, certificatePath));

    private static void SaveBytes(string outputPath, byte[] bytes)
    {
        try
        {
            // Overwrites file if it already exists (and can be overwritten)
            File.WriteAllBytes(outputPath, bytes);
        }
        catch (Exception e) when (e.IsIORelatedException)
        {
            throw new BuildFailedException(
                string.Format(CultureInfo.InvariantCulture, Strings.CouldNotWriteFileFmt, outputPath, e.Message),
                e);
        }
    }
}
