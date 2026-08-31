// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using Buildvana.Core;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Json;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Reads the pins <c>global.json</c> states: the .NET SDK baseline of the <c>netsdk</c> scope, and the
/// MSBuild project SDKs of the <c>sdks</c> scope.
/// </summary>
/// <remarks>
/// <para>An absent file, an absent section, or an absent member is not a problem: the repository pins
/// nothing there, the report says so, and nothing is created.</para>
/// <para>A member whose value is not a string is read as no pin either. <c>global.json</c> is the .NET CLI's
/// file, and what it makes of a malformed one is its own business, not <c>bv dependencies</c>'s.</para>
/// </remarks>
internal sealed class GlobalJsonPinReader(IHomeDirectoryProvider home, IJsonHelper jsonHelper)
{
    /// <summary>
    /// The path of <c>global.json</c>, relative to the home directory.
    /// </summary>
    public const string RelativePath = "global.json";

    private const string SdkSectionName = "sdk";
    private const string VersionMemberName = "version";
    private const string AllowPrereleaseMemberName = "allowPrerelease";
    private const string MsBuildSdksSectionName = "msbuild-sdks";

    /// <summary>
    /// Reads the pins of <c>global.json</c>.
    /// </summary>
    /// <returns>What the file pins; empty when there is no file.</returns>
    /// <exception cref="BuildFailedException">The file exists and could not be read or parsed.</exception>
    public GlobalJsonPins Read()
    {
        var path = home.GetFullPath(RelativePath);
        if (!File.Exists(path))
        {
            return new GlobalJsonPins(null, []);
        }

        var root = jsonHelper.LoadObject(path);
        return new GlobalJsonPins(ReadNetSdk(root), ReadSdks(root));
    }

    private static NetSdkPin? ReadNetSdk(JsonObject root)
    {
        if (root[SdkSectionName] is not JsonObject sdk || ReadString(sdk, VersionMemberName) is not { } version)
        {
            return null;
        }

        return NetSdkPin.Create(version, ReadBoolean(sdk, AllowPrereleaseMemberName));
    }

    private static List<DependencyPin> ReadSdks(JsonObject root)
    {
        if (root[MsBuildSdksSectionName] is not JsonObject sdks)
        {
            return [];
        }

        var pins = new List<DependencyPin>();
        foreach (var (id, node) in sdks)
        {
            if (node is JsonValue value && value.TryGetValue<string>(out var version) && !BuildvanaFamily.Contains(id))
            {
                pins.Add(DependencyPin.Create(DependencyScope.Sdks, id, version, RelativePath));
            }
        }

        return pins;
    }

    private static string? ReadString(JsonObject parent, string name)
        => parent[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static bool? ReadBoolean(JsonObject parent, string name)
        => parent[name] is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : null;
}
