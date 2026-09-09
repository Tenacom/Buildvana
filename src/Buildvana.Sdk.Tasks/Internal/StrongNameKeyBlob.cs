// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Buildvana.Sdk.Internal;

/// <summary>
/// Writes an RSA private key in the layout of a strong-name key file, as <c>sn -k</c> does.
/// </summary>
internal static class StrongNameKeyBlob
{
    // The layout is the PRIVATEKEYBLOB of the Microsoft base cryptographic provider:
    // https://learn.microsoft.com/en-us/windows/win32/seccrypto/base-provider-key-blobs
    // A BLOBHEADER, an RSAPUBKEY, then modulus, prime1, prime2, exponent1, exponent2, coefficient, and
    // privateExponent, each little-endian. The primes and the CRT values take half the modulus length.
    // The compiler reads the same layout in
    // https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/StrongName/CryptoBlobParser.cs
    private const byte PrivateKeyBlobType = 0x07; // PRIVATEKEYBLOB
    private const byte BlobVersion = 0x02; // CUR_BLOB_VERSION
    private const uint RsaSignAlgorithmId = 0x00002400; // CALG_RSA_SIGN
    private const uint Rsa2Magic = 0x32415352; // "RSA2"
    private const int HeaderLength = 8 + 12; // BLOBHEADER + RSAPUBKEY

    /// <summary>
    /// Writes the private key blob of an RSA key.
    /// </summary>
    /// <param name="parameters">The RSA parameters, private parameters included.</param>
    /// <returns>The blob.</returns>
    /// <exception cref="ArgumentException">
    /// A parameter is missing, or a value does not fit the slot the modulus length gives it.
    /// </exception>
    public static byte[] Write(RSAParameters parameters)
    {
        var modulus = TrimLeadingZeros(Require(parameters.Modulus, nameof(parameters.Modulus)));
        var modulusLength = modulus.Length;
        var halfLength = (modulusLength + 1) / 2;
        var blob = new byte[HeaderLength + (modulusLength * 2) + (halfLength * 5)];
        var span = blob.AsSpan();
        span[0] = PrivateKeyBlobType;
        span[1] = BlobVersion;
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], RsaSignAlgorithmId);
        BinaryPrimitives.WriteUInt32LittleEndian(span[8..], Rsa2Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(span[12..], (uint)(modulusLength * 8));
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], ToUInt32(Require(parameters.Exponent, nameof(parameters.Exponent))));

        var fields = span[HeaderLength..];
        WriteLittleEndian(modulus, Take(ref fields, modulusLength));
        WriteLittleEndian(Require(parameters.P, nameof(parameters.P)), Take(ref fields, halfLength));
        WriteLittleEndian(Require(parameters.Q, nameof(parameters.Q)), Take(ref fields, halfLength));
        WriteLittleEndian(Require(parameters.DP, nameof(parameters.DP)), Take(ref fields, halfLength));
        WriteLittleEndian(Require(parameters.DQ, nameof(parameters.DQ)), Take(ref fields, halfLength));
        WriteLittleEndian(Require(parameters.InverseQ, nameof(parameters.InverseQ)), Take(ref fields, halfLength));
        WriteLittleEndian(Require(parameters.D, nameof(parameters.D)), Take(ref fields, modulusLength));
        return blob;
    }

    private static byte[] Require(byte[]? value, string name)
        => value ?? throw new ArgumentException($"The {name} parameter is missing.", name);

    private static ReadOnlySpan<byte> TrimLeadingZeros(ReadOnlySpan<byte> bigEndian)
        => bigEndian.TrimStart((byte)0);

    private static uint ToUInt32(ReadOnlySpan<byte> bigEndian)
    {
        var value = TrimLeadingZeros(bigEndian);
        if (value.Length > sizeof(uint))
        {
            throw new ArgumentException("The public exponent does not fit 32 bits.", nameof(bigEndian));
        }

        var result = 0u;
        foreach (var b in value)
        {
            result = (result << 8) | b;
        }

        return result;
    }

    private static Span<byte> Take(ref Span<byte> buffer, int length)
    {
        var taken = buffer[..length];
        buffer = buffer[length..];
        return taken;
    }

    // Writes a big-endian unsigned value into a zeroed little-endian slot. The value may be shorter than the slot.
    private static void WriteLittleEndian(ReadOnlySpan<byte> bigEndian, Span<byte> slot)
    {
        var value = TrimLeadingZeros(bigEndian);
        if (value.Length > slot.Length)
        {
            throw new ArgumentException("The value does not fit its slot in the key blob.", nameof(bigEndian));
        }

        for (var i = 0; i < value.Length; i++)
        {
            slot[i] = value[value.Length - 1 - i];
        }
    }
}
