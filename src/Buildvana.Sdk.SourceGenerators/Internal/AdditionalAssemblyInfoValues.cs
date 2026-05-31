// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Sdk.SourceGenerators.Internal;

// ReSharper disable once InconsistentNaming - CLSCompliant is the actual name of the attribute, and it's more readable to keep it as-is in this context.
internal readonly record struct AdditionalAssemblyInfoValues(bool? CLSCompliant, bool? ComVisible);
