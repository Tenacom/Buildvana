// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

/// <summary>
/// A release asset as recorded by <see cref="RecordingServerRelease"/>.
/// </summary>
/// <param name="Path">The path of the asset's file.</param>
/// <param name="Description">The description of the asset.</param>
/// <param name="MimeType">The MIME type of the asset.</param>
/// <remarks>
/// <para>This mirrors the release's own <c>AssetData</c>, which is a protected nested type and therefore
/// cannot travel through a public member of the fake.</para>
/// </remarks>
internal sealed record RecordedAsset(string Path, string Description, string MimeType);
