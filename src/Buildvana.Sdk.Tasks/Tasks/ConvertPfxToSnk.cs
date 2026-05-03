// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Buildvana.Core;
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
        BuildFailedException.ThrowIfNot(
            !string.IsNullOrEmpty(PfxPath),
            string.Format(CultureInfo.InvariantCulture, Strings.MissingParameterFmt, nameof(PfxPath)));
        BuildFailedException.ThrowIfNot(
            !string.IsNullOrEmpty(OutputPath),
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
            throw new BuildFailedException(string.Format(CultureInfo.InvariantCulture, Strings.AssemblySigning.CannotExtractCertificateFmt, path));
        }
    }

    private static byte[] ExtractPrivateKey(X509Certificate2 certificate, string certificatePath)
        => certificate.GetRSAPrivateKey() is RSACryptoServiceProvider privateKey
            ? privateKey.ExportCspBlob(true)
            : throw new BuildFailedException(string.Format(CultureInfo.InvariantCulture, Strings.AssemblySigning.MissingRsaPrivateKeyFmt, certificatePath));

    private static void SaveBytes(string outputPath, byte[] bytes)
    {
        try
        {
            // Overwrites file if it already exists (and can be overwritten)
            File.WriteAllBytes(outputPath, bytes);
        }
        catch (Exception e) when (e.IsIORelatedException())
        {
            throw new BuildFailedException(string.Format(CultureInfo.InvariantCulture, Strings.CouldNotWriteFileFmt, outputPath, e.Message), e);
        }
    }
}
