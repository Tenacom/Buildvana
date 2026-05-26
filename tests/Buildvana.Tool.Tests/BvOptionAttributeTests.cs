// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using Buildvana.Tool.CommandLine;

internal sealed class BvOptionAttributeTests
{
    [BvOption("-c|--configuration <NAME>")]
    private string? SampleValueOption { get; init; }

    [Test]
    public async Task ValueOptionTemplate_ParsesNamesAndValuePlaceholder()
    {
        var attribute = new BvOptionAttribute("-c|--configuration <NAME>");
        await Assert.That(attribute.Template).IsEqualTo("-c|--configuration <NAME>");
        await Assert.That(attribute.ShortNames.Count).IsEqualTo(1);
        await Assert.That(attribute.ShortNames[0]).IsEqualTo("-c");
        await Assert.That(attribute.LongNames.Count).IsEqualTo(1);
        await Assert.That(attribute.LongNames[0]).IsEqualTo("--configuration");
        await Assert.That(attribute.ValueName).IsEqualTo("NAME");
    }

    [Test]
    public async Task FlagTemplate_ParsesLongNameAndNoValuePlaceholder()
    {
        var attribute = new BvOptionAttribute("--no-color");
        await Assert.That(attribute.ShortNames.Count).IsEqualTo(0);
        await Assert.That(attribute.LongNames.Count).IsEqualTo(1);
        await Assert.That(attribute.LongNames[0]).IsEqualTo("--no-color");
        await Assert.That(attribute.ValueName).IsNull();
    }

    [Test]
    public async Task Attribute_IsDiscoverableViaReflection()
    {
        var property = typeof(BvOptionAttributeTests).GetProperty(nameof(SampleValueOption), BindingFlags.Instance | BindingFlags.NonPublic);
        var attribute = property!.GetCustomAttribute<BvOptionAttribute>();
        await Assert.That(attribute).IsNotNull();
        await Assert.That(attribute!.ValueName).IsEqualTo("NAME");
    }
}
