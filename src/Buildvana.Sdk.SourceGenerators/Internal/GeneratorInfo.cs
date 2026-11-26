// Copyright (C) Tenacom and contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Sdk.SourceGenerators.Internal;

/// <summary>
/// Provides the identity of the package that the generated code comes from.
/// </summary>
internal static class GeneratorInfo
{
    /// <summary>
    /// The id of the package that ships the source generators.
    /// </summary>
    public const string PackageId = "Buildvana.Sdk";

    /// <summary>
    /// The version of the package that ships the source generators.
    /// </summary>
    public const string PackageVersion = ThisAssembly.AssemblyInformationalVersion;
}
