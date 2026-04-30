// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace Buildvana.Sdk.Resources;

/// <summary>
/// Provides common strings and composite formats for the project.
/// </summary>
internal static partial class Strings
{
    public static readonly CompositeFormat MissingParameterFmt = CompositeFormat.Parse("BVSDK1050: Parameter '{0}' is missing or empty.");
    public static readonly CompositeFormat CouldNotWriteFileFmt = CompositeFormat.Parse("BVSDK1051: The file '{0}' could not be created. {1}");
}
