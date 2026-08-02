// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.Build.Locator;

// Registers the MSBuild instance before any Microsoft.Build assembly is loaded.
// A module initializer runs before any other code in the assembly, which is exactly
// the ordering MSBuildLocator requires.
internal static class MSBuildBootstrap
{
    [ModuleInitializer]
    internal static void Initialize() => MSBuildLocator.RegisterDefaults();
}
