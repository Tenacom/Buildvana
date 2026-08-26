// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Utilities;

/// <summary>
/// The kind of a managed file-based-app directive.
/// </summary>
internal enum AppDirectiveKind
{
    /// <summary>
    /// A <c>#:package</c> directive: a NuGet package reference.
    /// </summary>
    Package,

    /// <summary>
    /// A <c>#:sdk</c> directive: an MSBuild project SDK reference.
    /// </summary>
    Sdk,
}
