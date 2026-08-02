// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.CommandLine;

internal sealed class BvArgumentAttributeTests
{
    [Test]
    public async Task RequiredTemplate_Parses()
    {
        var attribute = new BvArgumentAttribute("<CHANGE>");
        await Assert.That(attribute.Name).IsEqualTo("CHANGE");
        await Assert.That(attribute.Required).IsTrue();
        await Assert.That(attribute.Template).IsEqualTo("<CHANGE>");
    }

    [Test]
    public async Task OptionalTemplate_Parses()
    {
        var attribute = new BvArgumentAttribute("[CHANGE]");
        await Assert.That(attribute.Name).IsEqualTo("CHANGE");
        await Assert.That(attribute.Required).IsFalse();
    }

    [Test]
    public async Task Template_ToleratesExcessWhitespace()
    {
        var attribute = new BvArgumentAttribute("  [ CHANGE ]  ");
        await Assert.That(attribute.Name).IsEqualTo("CHANGE");
        await Assert.That(attribute.Required).IsFalse();
    }

    [Test]
    [Arguments("CHANGE")]
    [Arguments("<CHANGE]")]
    [Arguments("<>")]
    [Arguments("[]")]
    public async Task MalformedTemplate_Throws(string template)
    {
        await Assert.That(() => new BvArgumentAttribute(template)).Throws<ArgumentException>();
    }
}
