// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Json;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Writes the .NET SDK baseline, and the setting that must agree with its policy.
/// </summary>
/// <remarks>
/// <para><c>sdk.allowPrerelease</c> is derived state: the policy is where the intent to follow previews is
/// stated, and the resolver of <c>global.json</c> must be told the same thing. An apply run writes it,
/// adding it when the file states none.</para>
/// <para>This is the last thing an apply run writes. A <c>global.json</c> naming an SDK that is not
/// installed breaks every later <c>dotnet</c> invocation, because <c>rollForward</c> never rolls down to an
/// older patch.</para>
/// </remarks>
internal sealed class NetSdkPinWriter(IHomeDirectoryProvider home, IJsonHelper jsonHelper, IReporter reporter)
{
    private const string SdkSectionName = "sdk";
    private const string VersionMemberName = "version";
    private const string AllowPrereleaseMemberName = "allowPrerelease";

    /// <summary>
    /// Writes the baseline and its setting.
    /// </summary>
    /// <param name="resolution">What the run made of the baseline.</param>
    /// <exception cref="BuildFailedException"><c>global.json</c> could not be read or written.</exception>
    public void Write(NetSdkResolution resolution)
    {
        Guard.IsNotNull(resolution);
        var path = home.GetFullPath(GlobalJsonPinReader.RelativePath);
        if (resolution.Target is { } target)
        {
            _ = jsonHelper.RewriteStringValues(
                path,
                (propertyPath, currentValue) => propertyPath is [SdkSectionName, VersionMemberName]
                    ? PinVersionText.Restate(currentValue, target)
                    : null);

            reporter.Detail($"Stated .NET SDK {target.ToNormalizedString()} in {GlobalJsonPinReader.RelativePath}.");
        }

        if (!resolution.WritesAllowPrerelease)
        {
            return;
        }

        var allowPrerelease = resolution.Policy.AllowPrerelease;
        if (resolution.Pin.AllowPrerelease is null)
        {
            _ = jsonHelper.InsertProperty(path, [SdkSectionName], AllowPrereleaseMemberName, JsonValue.Create(allowPrerelease));
        }
        else
        {
            _ = jsonHelper.RewriteBooleanValues(
                path,
                (propertyPath, _) => propertyPath is [SdkSectionName, AllowPrereleaseMemberName] ? allowPrerelease : null);
        }

        reporter.Detail($"Stated {AllowPrereleaseMemberName} as {allowPrerelease} in {GlobalJsonPinReader.RelativePath}.");
    }
}
