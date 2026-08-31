// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// One kind of dependency <c>bv dependencies</c> manages, together with the files that record it.
/// </summary>
internal enum DependencyScope
{
    /// <summary>The .NET SDK version, in the <c>sdk</c> section of <c>global.json</c>.</summary>
    NetSdk,

    /// <summary>
    /// The MSBuild project SDKs, in the <c>msbuild-sdks</c> section of <c>global.json</c> and in the
    /// <c>#:sdk</c> directives of file-based apps.
    /// </summary>
    Sdks,

    /// <summary>The .NET local tools, in the tool manifest.</summary>
    Tools,

    /// <summary>
    /// The NuGet package pins: the ones MSBuild evaluates, the ones an additional group declares, and the
    /// <c>#:package</c> directives of file-based apps.
    /// </summary>
    Packages,
}
