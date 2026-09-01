// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

internal sealed partial class OverrideLifecycle
{
    // What a run has selected so far, across every pass of it. Each pass writes the union rather than its own
    // findings alone: the second pass's graph no longer reports what the first one lifted, and writing only
    // the latest findings would drop that override and bring the vulnerability back.
    private sealed class RunState
    {
        private readonly Dictionary<string, NuGetVersion> _central = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, NuGetVersion?>> _promotions = new(StringComparer.OrdinalIgnoreCase);

        // Whether anything at all has been selected, which is also whether any file will exist after a write.
        public bool HasAny => _central.Count > 0 || _promotions.Values.Any(static promotions => promotions.Count > 0);

        public bool HasPromotion(string projectFullPath, string packageId)
            => _promotions.TryGetValue(projectFullPath, out var promotions) && promotions.ContainsKey(packageId);

        // Two projects needing one package take the higher of the two versions: a version is a floor, so the
        // higher one satisfies both, and one central entry answers for every project.
        public void AddCentral(string packageId, NuGetVersion version)
        {
            var isHigher = !_central.TryGetValue(packageId, out var known)
                || VersionComparer.VersionRelease.Compare(version, known) > 0;

            if (isHigher)
            {
                _central[packageId] = version;
            }
        }

        public void AddPromotion(string projectFullPath, string packageId, NuGetVersion? version)
        {
            if (!_promotions.TryGetValue(projectFullPath, out var promotions))
            {
                promotions = new Dictionary<string, NuGetVersion?>(StringComparer.OrdinalIgnoreCase);
                _promotions[projectFullPath] = promotions;
            }

            var isHigher = !promotions.TryGetValue(packageId, out var known)
                || (version is not null && (known is null || VersionComparer.VersionRelease.Compare(version, known) > 0));

            if (isHigher)
            {
                promotions[packageId] = version;
            }
        }

        // Every project of the solution is in the plan, including the ones with nothing to promote: that is
        // what removes a file an earlier run left behind.
        public TransitiveOverridePlan ToPlan(IReadOnlyList<OverrideProject> projects)
            => new()
            {
                Central = [.. _central.Select(static entry => new PackageOverride(entry.Key, entry.Value))],
                Projects = [.. projects.Select(project => new ProjectOverrides
                {
                    ProjectFullPath = project.ProjectFullPath,
                    Promotions = PromotionsOf(project.ProjectFullPath),
                })],
            };

        private IReadOnlyList<PackageOverride> PromotionsOf(string projectFullPath)
            => _promotions.TryGetValue(projectFullPath, out var promotions)
                ? [.. promotions.Select(static entry => new PackageOverride(entry.Key, entry.Value))]
                : [];
    }
}
