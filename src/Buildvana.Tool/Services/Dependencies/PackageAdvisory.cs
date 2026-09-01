// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using NuGet.Protocol;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// One security advisory a package source states about a package.
/// </summary>
/// <param name="Url">Where the advisory is described.</param>
/// <param name="Severity">The severity the source gives it.</param>
/// <param name="AffectedVersions">The versions of the package the advisory covers.</param>
/// <remarks>
/// <para>The severity is NuGet's own, and it is compared with the severity a project audits from, which is
/// that project's evaluated <c>NuGetAuditLevel</c>.</para>
/// <para>An advisory states the versions it covers and no fixed version, so the version that fixes it is the
/// first one the affected ranges of every advisory leave out.</para>
/// </remarks>
internal sealed record PackageAdvisory(Uri Url, PackageVulnerabilitySeverity Severity, VersionRange AffectedVersions);
