// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Buildvana.Core.Dependencies;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Reads the evaluations a pin dump answered with.
/// </summary>
internal static class PackagePinDumps
{
    /// <summary>
    /// Pairs each evaluation that states an assets file with the path of that file.
    /// </summary>
    /// <param name="evaluations">The evaluations the pin dump answered with.</param>
    /// <returns>The evaluations that state an assets file, each with its path, in the order given. An
    /// evaluation that states none is left out: Buildvana SDK never reached its project.</returns>
    public static IEnumerable<(PackagePinDump Evaluation, string AssetsFilePath)> WithAssetsFile(
        IEnumerable<PackagePinDump> evaluations)
    {
        foreach (var evaluation in evaluations)
        {
            if (evaluation.ProjectAssetsFile is { Length: > 0 } assetsFilePath)
            {
                yield return (evaluation, assetsFilePath);
            }
        }
    }
}
