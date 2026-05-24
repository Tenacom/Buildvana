// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.Configuration;

/// <summary>
/// Specifies which releases require an entry in the changelog.
/// </summary>
public enum ChangelogUpdates
{
    /// <summary>The changelog is never required to be updated.</summary>
    None,

    /// <summary>The changelog must be updated for stable releases only.</summary>
    Stable,

    /// <summary>The changelog must be updated for every release, including prereleases.</summary>
    All,
}
