// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Buildvana.Core.Configuration;

// The policy strings the schema enumerates are spelled out in a constant, an attribute argument having to be
// one. These tests derive the same lists from the enums, through the formatter that parsing round-trips, so a
// kind added to either enum without a matching schema value fails here.
internal sealed class BuildvanaJsonConfigSchemaTests
{
    private static readonly JsonNode Dependencies
        = JsonNode.Parse(BuildvanaJsonConfigSchema.Generate())!["properties"]!["dependencies"]!["properties"]!;

    [Test]
    public async Task Generate_NetSdkScope_EnumeratesEveryNetSdkPolicyString()
    {
        var netSdk = AllowedValues(Dependencies["scopes"]!["properties"]!["netsdk"]);

        await Assert.That(netSdk.SequenceEqual(NetSdkPolicyStrings())).IsTrue();
    }

    [Test]
    public async Task Generate_PackagePositions_EnumerateEveryPackagePolicyString()
    {
        var expected = PackagePolicyStrings();
        var scopes = Dependencies["scopes"]!["properties"]!;

        await Assert.That(AllowedValues(scopes["sdks"]).SequenceEqual(expected)).IsTrue();
        await Assert.That(AllowedValues(scopes["tools"]).SequenceEqual(expected)).IsTrue();
        await Assert.That(AllowedValues(scopes["packages"]).SequenceEqual(expected)).IsTrue();
        await Assert.That(AllowedValues(Dependencies["policies"]!["additionalProperties"]).SequenceEqual(expected)).IsTrue();
        await Assert.That(AllowedValues(GroupProperties()["policy"]).SequenceEqual(expected)).IsTrue();
    }

    // The caption travels as the member name, so the value object must not restate it.
    [Test]
    public async Task Generate_AdditionalPackageGroup_ForbidsTheCaptionInsideTheValue()
        => await Assert.That(GroupProperties()["caption"]!.GetValueKind()).IsEqualTo(JsonValueKind.False);

    private static List<string> NetSdkPolicyStrings()
    {
        var strings = new List<string>();
        foreach (var kind in Enum.GetValues<NetSdkUpdatePolicyKind>())
        {
            strings.Add(new NetSdkUpdatePolicy(kind, AllowPrerelease: false).ToString());
            strings.Add(new NetSdkUpdatePolicy(kind, AllowPrerelease: true).ToString());
        }

        return strings;
    }

    private static List<string> PackagePolicyStrings()
    {
        var strings = new List<string>();
        foreach (var kind in Enum.GetValues<PackageUpdatePolicyKind>())
        {
            strings.Add(new PackageUpdatePolicy(kind, AllowPrerelease: false).ToString());
            strings.Add(new PackageUpdatePolicy(kind, AllowPrerelease: true).ToString());
        }

        return strings;
    }

    private static JsonNode GroupProperties()
        => Dependencies["additionalPackages"]!["additionalProperties"]!["properties"]!;

    private static List<string> AllowedValues(JsonNode? schema)
        => [.. ((JsonArray)schema!["enum"]!).Select(static value => value!.GetValue<string>())];
}
