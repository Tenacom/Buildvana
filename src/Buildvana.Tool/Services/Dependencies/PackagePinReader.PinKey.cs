// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Dependencies;

partial class PackagePinReader
{
    /// <summary>
    /// What makes two evaluated items one pin: the file that declares them, the item type, the id, and the
    /// version text, which is what tells two target-framework-conditioned declarations apart.
    /// </summary>
    /// <param name="DeclaringFile">The path of the declaring file, relative to the home directory.</param>
    /// <param name="ItemType">The MSBuild item type the pin is declared as.</param>
    /// <param name="Id">The package id.</param>
    /// <param name="VersionText">The version text, as the declaring file states it.</param>
    private readonly record struct PinKey(string DeclaringFile, string ItemType, string Id, string VersionText);
}
