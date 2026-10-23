// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Release;

/// <summary>
/// Describes a file attached to a release.
/// </summary>
/// <param name="Path">The path of the file.</param>
/// <param name="Description">A short description of the file, which becomes its label on the host.</param>
/// <param name="MimeType">The MIME type of the file.</param>
internal sealed record ReleaseAsset(string Path, string Description, string MimeType);
