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
    public async Task PlainTemplate_IsNotVariadic()
    {
        var attribute = new BvArgumentAttribute("[CHANGE]");
        await Assert.That(attribute.Variadic).IsFalse();
    }

    [Test]
    public async Task OptionalVariadicTemplate_Parses()
    {
        var attribute = new BvArgumentAttribute("[ID...]");
        await Assert.That(attribute.Name).IsEqualTo("ID");
        await Assert.That(attribute.Required).IsFalse();
        await Assert.That(attribute.Variadic).IsTrue();
    }

    [Test]
    public async Task RequiredVariadicTemplate_Parses()
    {
        var attribute = new BvArgumentAttribute("<ID...>");
        await Assert.That(attribute.Name).IsEqualTo("ID");
        await Assert.That(attribute.Required).IsTrue();
        await Assert.That(attribute.Variadic).IsTrue();
    }

    [Test]
    public async Task VariadicTemplate_ToleratesExcessWhitespace()
    {
        var attribute = new BvArgumentAttribute("  [ ID ... ]  ");
        await Assert.That(attribute.Name).IsEqualTo("ID");
        await Assert.That(attribute.Variadic).IsTrue();
    }

    [Test]
    [Arguments("CHANGE")]
    [Arguments("<CHANGE]")]
    [Arguments("<>")]
    [Arguments("[]")]
    [Arguments("[...]")]
    public async Task MalformedTemplate_Throws(string template)
    {
        await Assert.That(() => new BvArgumentAttribute(template)).Throws<ArgumentException>();
    }
}
