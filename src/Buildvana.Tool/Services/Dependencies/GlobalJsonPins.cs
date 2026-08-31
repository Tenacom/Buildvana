// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// What <c>global.json</c> pins: the .NET SDK baseline, and the MSBuild project SDKs.
/// </summary>
/// <param name="NetSdk">The .NET SDK baseline, or <see langword="null"/> when the file states none.</param>
/// <param name="Sdks">The MSBuild project SDK pins, in the order the file states them. The Buildvana SDK is
/// not among them: it is a family pin, and <c>bv self-update</c> moves it.</param>
internal sealed record GlobalJsonPins(NetSdkPin? NetSdk, IReadOnlyList<DependencyPin> Sdks);
