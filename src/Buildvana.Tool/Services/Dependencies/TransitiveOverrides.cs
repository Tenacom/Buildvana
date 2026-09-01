// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// What the transitive override mechanism is made of, where more than one part of <c>bv</c> needs to know it.
/// </summary>
/// <remarks>
/// <para>The Buildvana SDK imports the override files, and <c>bv</c> alone decides what they hold. The
/// property named here is the whole of the SDK's side of the conversation, and it is documented as an
/// internal-use property in <c>docs/InternalUseProperties.md</c>.</para>
/// </remarks>
internal static class TransitiveOverrides
{
    /// <summary>
    /// The MSBuild property that tells the Buildvana SDK to leave the override files out of an evaluation.
    /// </summary>
    /// <remarks>
    /// <para>Suppressing the import is how the graph as it stands without overrides is obtained. It is
    /// preferred to deleting the files, which would leave an interrupted run with nothing to put back.</para>
    /// </remarks>
    public const string SuppressionProperty = "BV_SuppressTransitiveOverrides";
}
