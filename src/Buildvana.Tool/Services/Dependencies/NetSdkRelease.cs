// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// A candidate .NET SDK release, as <see cref="UpdatePolicyEngine"/> needs to see it.
/// </summary>
/// <param name="Version">The SDK version.</param>
/// <param name="IsLts"><see langword="true"/> if the release belongs to a long-term support channel.</param>
/// <remarks>
/// <para>Whether a release is long-term support comes from the release type of its channel in the .NET
/// release index.</para>
/// </remarks>
internal sealed record NetSdkRelease(NuGetVersion Version, bool IsLts);
