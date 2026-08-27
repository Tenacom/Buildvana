// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.Configuration;

/// <summary>
/// The update policy of the .NET SDK baseline version: how far an automatic update may move the pin, and
/// whether it may land on a prerelease version.
/// </summary>
/// <param name="Kind">How far an automatic update may move the pin.</param>
/// <param name="AllowPrerelease"><see langword="true"/> if an update may land on a prerelease version;
/// <see langword="false"/> if prerelease versions are never candidates.</param>
public readonly record struct NetSdkUpdatePolicy(NetSdkUpdatePolicyKind Kind, bool AllowPrerelease)
{
    /// <summary>
    /// Parses a policy string: a <see cref="NetSdkUpdatePolicyKind"/> name, matched case-insensitively,
    /// optionally followed by a <c>-</c> meaning that prerelease versions are allowed.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="result">When this method returns <see langword="true"/>, the parsed policy; otherwise,
    /// the default policy, which updates nothing.</param>
    /// <returns><see langword="true"/> if <paramref name="text"/> is a policy string; otherwise,
    /// <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>There is no <c>Parse</c> counterpart on purpose. An unparseable policy string is a user error
    /// that must be reported together with the file and the item that carried it, and neither is known
    /// here.</para>
    /// </remarks>
    public static bool TryParse(string? text, out NetSdkUpdatePolicy result)
    {
        if (!UpdatePolicySyntax.TryParse<NetSdkUpdatePolicyKind>(text, out var kind, out var allowPrerelease))
        {
            result = default;
            return false;
        }

        result = new NetSdkUpdatePolicy(kind, allowPrerelease);
        return true;
    }

    /// <summary>
    /// Returns the policy string this policy parses from, e.g. <c>lts</c> or <c>lts-</c>.
    /// </summary>
    /// <returns>The policy string.</returns>
    public override string ToString() => UpdatePolicySyntax.Format(Kind, AllowPrerelease);
}
