// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

// A PackageReference item as evaluated: its Version and IsImplicitlyDefined metadata are empty when absent.
internal sealed record SdkPackageReference(string Id, string Version, string IsImplicitlyDefined);
