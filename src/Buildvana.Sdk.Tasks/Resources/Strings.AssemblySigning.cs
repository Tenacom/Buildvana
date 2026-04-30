// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace Buildvana.Sdk.Resources;

partial class Strings
{
    public static class AssemblySigning
    {
        public static readonly CompositeFormat CannotExtractCertificateFmt = CompositeFormat.Parse("BVSDK1201: Cannot extract certificate from '{0}'.");
        public static readonly CompositeFormat MissingRsaPrivateKeyFmt = CompositeFormat.Parse("BVSDK1202: '{0}' does not contain an exportable RSA private key.");
    }
}
