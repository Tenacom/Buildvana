// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

extern alias Generators;

using Generators::Buildvana.Sdk.SourceGenerators.Internal;

internal sealed class ConstantValueParserTests
{
    [Test]
    [Arguments(null)]
    [Arguments("")]
    public async Task TryParse_EmptyValue_YieldsNull(string? str)
    {
        var success = ConstantValueParser.TryParse(str, out var result);
        await Assert.That(success).IsTrue();
        await Assert.That(result).IsNull();
    }

    [Test]
    [Arguments("\"\"", "")]
    [Arguments("\"foo\"", "foo")]
    [Arguments("\"\"\"Murder\"\", she wrote\"", "\"Murder\", she wrote")]
    [Arguments("\" spaces kept \"", " spaces kept ")]
    public async Task TryParse_QuotedString_YieldsUnquotedString(string str, string expected)
    {
        var success = ConstantValueParser.TryParse(str, out var result);
        await Assert.That(success).IsTrue();
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments("byte:200", (byte)200)]
    [Arguments("uint8:200", (byte)200)]
    [Arguments("System.Byte:200", (byte)200)]
    [Arguments("short:-5", (short)-5)]
    [Arguments("int16:-5", (short)-5)]
    [Arguments("System.Int16:-5", (short)-5)]
    [Arguments("int:42", 42)]
    [Arguments("int32:42", 42)]
    [Arguments("System.Int32:42", 42)]
    [Arguments("long:42", 42L)]
    [Arguments("int64:42", 42L)]
    [Arguments("System.Int64:42", 42L)]
    [Arguments("bool:true", true)]
    [Arguments("System.Boolean:false", false)]
    [Arguments("string:foo", "foo")]
    [Arguments("System.String:foo", "foo")]
    [Arguments("string:", "")]
    [Arguments("string:int:42", "int:42")]
    [Arguments("INT:42", 42)]
    [Arguments("String:foo", "foo")]
    [Arguments(" int :42", 42)]
    public async Task TryParse_TypedValue_YieldsTypedResult(string str, object? expected)
    {
        var success = ConstantValueParser.TryParse(str, out var result);
        await Assert.That(success).IsTrue();
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments("42", 42)]
    [Arguments("-13", -13)]
    [Arguments("9999999999", 9999999999L)]
    [Arguments("-9999999999", -9999999999L)]
    [Arguments("99998888777766665555", "99998888777766665555")] // too large even for long, so it stays a string
    [Arguments("true", true)]
    [Arguments("False", false)]
    [Arguments("foo", "foo")]
    [Arguments("false90", "false90")]
    [Arguments(":foo", ":foo")]
    public async Task TryParse_UntypedValue_GuessesType(string str, object expected)
    {
        var success = ConstantValueParser.TryParse(str, out var result);
        await Assert.That(success).IsTrue();
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments("integer:42")]
    [Arguments("boolean:true")]
    [Arguments("unknown:42")]
    [Arguments("int:foo")]
    [Arguments("byte:300")]
    [Arguments("bool:yes")]
    public async Task TryParse_InvalidValue_Fails(string str)
    {
        var success = ConstantValueParser.TryParse(str, out var result);
        await Assert.That(success).IsFalse();
        await Assert.That(result).IsNull();
    }
}
