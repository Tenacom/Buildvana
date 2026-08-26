// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Utilities;

/// <summary>
/// A managed directive found in a file-based app's leading directive block.
/// </summary>
/// <param name="Kind">The directive's kind.</param>
/// <param name="Id">The package or SDK id, as spelled in the file. NuGet package ids are case-insensitive;
/// compare accordingly.</param>
/// <param name="VersionText">The text after the <c>@</c> separator, trimmed, or <see langword="null"/> when
/// the directive has no separator. A versionless <c>#:package</c> resolves through central package
/// management and is a reference to a pin, not a pin itself. The text is empty when the separator has
/// nothing after it, and is otherwise not guaranteed to parse as a version.</param>
// ReSharper disable once NotAccessedPositionalProperty.Global // First direct reader is bv deps's scope classification (issue #352 stage 2)
internal sealed record AppDirective(AppDirectiveKind Kind, string Id, string? VersionText);
