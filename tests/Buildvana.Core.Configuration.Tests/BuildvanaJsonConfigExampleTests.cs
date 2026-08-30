// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using Buildvana.Core.Configuration;

// The generator and the helpers behind it, on schemas written for the purpose. Every refusal the generator
// states is of a shape the model does not have, and every helper reports nothing on the model as it stands,
// so the committed example reaches neither. Both are reached here directly.
internal sealed class BuildvanaJsonConfigExampleTests
{
    // A description introduces its member, a section holds its members, and a member list ends in a comma
    // however deep it sits.
    [Test]
    public async Task Generate_Schema_WalksItIntoAnObject()
    {
        var root = new JsonObject
        {
            ["properties"] = new JsonObject
            {
                ["flag"] = new JsonObject { ["description"] = "Whether the thing is on.", ["default"] = true },
                ["section"] = new JsonObject
                {
                    ["properties"] = new JsonObject
                    {
                        ["name"] = new JsonObject { ["examples"] = new JsonArray { "value" } },
                    },
                },
            },
        };

        await Assert.That(BodyOf(root)).IsEqualTo(
            """
            {
              // Whether the thing is on.
              "flag": true,

              "section": {
                "name": "value",
              },
            }

            """);
    }

    // A container with nothing to show still has a shape to show.
    [Test]
    public async Task Generate_EmptyContainers_WriteTheirShape()
    {
        var root = new JsonObject
        {
            ["properties"] = new JsonObject
            {
                ["list"] = new JsonObject { ["type"] = "array" },
                ["map"] = new JsonObject { ["type"] = "object" },
            },
        };

        await Assert.That(BodyOf(root)).IsEqualTo(
            """
            {
              "list": [],

              "map": {},
            }

            """);
    }

    [Test]
    public async Task Generate_Reference_WritesWhatItPointsAt()
    {
        var root = new JsonObject
        {
            ["$defs"] = new JsonObject { ["shared"] = new JsonObject { ["default"] = 42 } },
            ["properties"] = new JsonObject { ["value"] = new JsonObject { ["$ref"] = "#/$defs/shared" } },
        };

        await Assert.That(BodyOf(root)).IsEqualTo(
            """
            {
              "value": 42,
            }

            """);
    }

    // An example on a section would print in place of the section, taking every member and every nested
    // description with it, and nothing downstream would notice the loss.
    [Test]
    public async Task Generate_ExampleBesideMembers_Throws()
    {
        var root = SchemaOf(
            "section",
            new JsonObject
            {
                ["examples"] = new JsonArray { "whole section" },
                ["properties"] = new JsonObject { ["member"] = new JsonObject { ["default"] = true } },
            });

        var problem = await ProblemOf(root).ConfigureAwait(false);

        await Assert.That(problem).IsEqualTo(
            "The setting 'section' states an example, and its value is a section: an object with members, "
            + "or a keyed object. The example would replace the whole section, and every description "
            + "inside. Annotate the members instead.");
    }

    // A keyed object is a section too: an example on one drops the member name it teaches, and the
    // description of what a member holds.
    [Test]
    public async Task Generate_ExampleBesideMemberNames_Throws()
    {
        var root = SchemaOf(
            "groups",
            new JsonObject
            {
                ["examples"] = new JsonArray { "whole section" },
                ["propertyNames"] = new JsonObject { ["examples"] = new JsonArray { "My group" } },
                ["additionalProperties"] = new JsonObject { ["type"] = "object" },
            });

        var problem = await ProblemOf(root).ConfigureAwait(false);

        await Assert.That(problem).IsEqualTo(
            "The setting 'groups' states an example, and its value is a section: an object with members, "
            + "or a keyed object. The example would replace the whole section, and every description "
            + "inside. Annotate the members instead.");
    }

    // The exporter deduplicates a member type that occurs more than once, and an annotation stays beside the
    // pointer rather than moving to the copy it points at. A section reached that way is a section still.
    [Test]
    public async Task Generate_ExampleOnAReferenceToASection_Throws()
    {
        var root = new JsonObject
        {
            ["$defs"] = new JsonObject
            {
                ["shared"] = new JsonObject
                {
                    ["properties"] = new JsonObject { ["member"] = new JsonObject { ["default"] = true } },
                },
            },
            ["properties"] = new JsonObject
            {
                ["section"] = new JsonObject
                {
                    ["examples"] = new JsonArray { "whole section" },
                    ["$ref"] = "#/$defs/shared",
                },
            },
        };

        var problem = await ProblemOf(root).ConfigureAwait(false);

        await Assert.That(problem).IsEqualTo(
            "The setting 'section' states an example, and its value is a section: an object with members, "
            + "or a keyed object. The example would replace the whole section, and every description "
            + "inside. Annotate the members instead.");
    }

    // A dictionary is the one object an example illustrates whole: its member names are data the schema
    // constrains in no way, so the example takes nothing away.
    [Test]
    public async Task Generate_ExampleOnADictionary_WritesTheExample()
    {
        var root = SchemaOf(
            "env",
            new JsonObject
            {
                ["type"] = "object",
                ["examples"] = new JsonArray { new JsonObject { ["KEY"] = "value" } },
                ["additionalProperties"] = new JsonObject { ["type"] = "string" },
            });

        await Assert.That(BodyOf(root)).IsEqualTo(
            """
            {
              "env": {"KEY": "value"},
            }

            """);
    }

    [Test]
    public async Task Generate_KeyedObjectWithoutAKeyExample_Throws()
    {
        var root = SchemaOf(
            "groups",
            new JsonObject
            {
                ["propertyNames"] = new JsonObject { ["type"] = "string" },
                ["additionalProperties"] = new JsonObject { ["type"] = "object" },
            });

        var problem = await ProblemOf(root).ConfigureAwait(false);

        await Assert.That(problem).IsEqualTo(
            "The keyed object 'groups' states no example member name. Annotate the key property of its "
            + "element type with [JsonSchemaExample].");
    }

    // The shape the generator emits for a keyed object whose key states nothing: an empty propertyNames, and
    // no keyword the walk answers before it. The empty node is what keeps the walk out of EmptyContainer,
    // which would print the whole section as {} — value schema, member descriptions and all — and report
    // nothing.
    [Test]
    public async Task Generate_KeyedObjectWithAnEmptyKeySchema_Throws()
    {
        var root = SchemaOf(
            "groups",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = new JsonObject { ["type"] = "object" },
                ["propertyNames"] = new JsonObject(),
            });

        var problem = await ProblemOf(root).ConfigureAwait(false);

        await Assert.That(problem).IsEqualTo(
            "The keyed object 'groups' states no example member name. Annotate the key property of its "
            + "element type with [JsonSchemaExample].");
    }

    [Test]
    public async Task Generate_KeyedObjectWithoutAValueSchema_Throws()
    {
        var root = SchemaOf(
            "groups",
            new JsonObject { ["propertyNames"] = new JsonObject { ["examples"] = new JsonArray { "My group" } } });

        var problem = await ProblemOf(root).ConfigureAwait(false);

        await Assert.That(problem).IsEqualTo("The keyed object 'groups' declares no value schema.");
    }

    // A setting the schema can supply no value for is a missing annotation on the model, which is a fault
    // of the model rather than a hole in the example.
    [Test]
    public async Task Generate_SettingWithNoValue_Throws()
    {
        var root = SchemaOf("setting", new JsonObject { ["type"] = "string" });

        var problem = await ProblemOf(root).ConfigureAwait(false);

        await Assert.That(problem).IsEqualTo(
            "The setting 'setting' states neither an example nor a default, and is not a container. "
            + "Annotate the model property with [JsonSchemaExample].");
    }

    [Test]
    public async Task Generate_OverlongDescription_Throws()
    {
        var description = Words(35);
        var root = SchemaOf("setting", new JsonObject { ["description"] = description, ["default"] = true });

        var problem = await ProblemOf(root).ConfigureAwait(false);

        await Assert.That(problem).IsEqualTo(
            $"The description \"{description}\" needs 3 comment lines, past the 2 a description gets. A "
            + "description names a setting; anything longer is documentation, and belongs in the reference "
            + "document.");
    }

    // The descriptions the model carries today, and the ones a reviewer would let through.
    [Test]
    public async Task DescriptionProblem_FittingDescription_ReportsNothing()
        => await Assert.That(BuildvanaJsonConfigExample.DescriptionProblem(Words(16) + "s")).IsNull();

    [Test]
    public async Task DescriptionProblem_TooManyLines_NamesTheCount()
    {
        var problem = BuildvanaJsonConfigExample.DescriptionProblem(Words(35));

        await Assert.That(problem).IsEqualTo("needs 3 comment lines, past the 2 a description gets");
    }

    // A word longer than the wrapped limit — a URL, say — produces a line the wrap cannot shorten. Counting
    // lines would call this description fine, because it still needs only two.
    [Test]
    public async Task DescriptionProblem_UnbreakableWord_NamesTheOverrun()
    {
        var problem = BuildvanaJsonConfigExample.DescriptionProblem("prefix " + new string('x', 90));

        await Assert.That(problem).IsEqualTo("holds a 90-character line, past the 72 a comment line gets");
    }

    // A description at the single-line limit is carried whole, however close it comes to the wrapped limit.
    [Test]
    public async Task WrapDescription_AtTheSingleLineLimit_ReturnsOneLine()
    {
        var description = Words(16) + "s";

        var lines = BuildvanaJsonConfigExample.WrapDescription(description);

        await Assert.That(description.Length).IsEqualTo(80);
        await Assert.That(lines.Count).IsEqualTo(1);
        await Assert.That(lines[0]).IsEqualTo(description);
    }

    // Past the single-line limit the narrower one governs, so a second line never carries a word or two.
    [Test]
    public async Task WrapDescription_PastTheSingleLineLimit_WrapsAtTheNarrowerLimit()
    {
        var lines = BuildvanaJsonConfigExample.WrapDescription(Words(17));

        await Assert.That(lines.Count).IsEqualTo(2);
        await Assert.That(lines.All(static line => line.Length <= 72)).IsTrue();
    }

    // Wrapping moves the line breaks and nothing else: no word is dropped, split, or reordered.
    [Test]
    public async Task WrapDescription_PreservesTheText()
    {
        var description = Words(17);

        var lines = BuildvanaJsonConfigExample.WrapDescription(description);

        await Assert.That(string.Join(' ', lines)).IsEqualTo(description);
    }

    // The wrap reports the lines it needs, however many. Refusing a third one is the caller's decision,
    // which it cannot make on a count that stops at two.
    [Test]
    public async Task WrapDescription_LongText_ReportsEveryLineItNeeds()
        => await Assert.That(BuildvanaJsonConfigExample.WrapDescription(Words(35)).Count).IsEqualTo(3);

    [Test]
    public async Task ResolveReference_FollowsADocumentLocalPointer()
    {
        var root = SampleSchema();

        var resolved = BuildvanaJsonConfigExample.ResolveReference(Reference("#/properties/args"), root, "args");

        await Assert.That(resolved["type"]!.GetValue<string>()).IsEqualTo("array");
    }

    // A member name holding a slash or a tilde is escaped in a pointer, and has to survive the trip back.
    [Test]
    public async Task ResolveReference_UnescapesPointerTokens()
    {
        var root = SampleSchema();

        var slash = BuildvanaJsonConfigExample.ResolveReference(Reference("#/properties/a~1b"), root, "a/b");
        var tilde = BuildvanaJsonConfigExample.ResolveReference(Reference("#/properties/c~0d"), root, "c~d");

        await Assert.That(slash["type"]!.GetValue<string>()).IsEqualTo("string");
        await Assert.That(tilde["type"]!.GetValue<string>()).IsEqualTo("boolean");
    }

    [Test]
    public async Task ResolveReference_UnresolvablePointer_Throws()
    {
        var root = SampleSchema();

        await Assert.That(() => BuildvanaJsonConfigExample.ResolveReference(Reference("#/properties/nope"), root, "nope"))
            .Throws<InvalidOperationException>();
    }

    // A pointer may name a member that is not an object, which is not a schema either. That is the same
    // failure as a pointer naming nothing, and it is reported the same way.
    [Test]
    public async Task ResolveReference_PointerToANonObject_Throws()
    {
        var root = SampleSchema();

        await Assert.That(() => BuildvanaJsonConfigExample.ResolveReference(Reference("#/properties/args/type"), root, "args"))
            .Throws<InvalidOperationException>();
    }

    // The object a schema describes, without the fixed header above it. The header holds no brace of its own.
    private static string BodyOf(JsonObject root)
    {
        var text = BuildvanaJsonConfigExample.Generate(root);

        return text[text.IndexOf('{', StringComparison.Ordinal)..];
    }

    // A schema document declaring one member, which is all a refusal needs to reach.
    private static JsonObject SchemaOf(string name, JsonObject member)
        => new() { ["properties"] = new JsonObject { [name] = member } };

    // The message the generator refuses a schema with.
    private static async Task<string> ProblemOf(JsonObject root)
    {
        string Act() => BuildvanaJsonConfigExample.Generate(root);

        var exception = await Assert.That(Act).Throws<InvalidOperationException>();

        return exception!.Message;
    }

    // Four-letter words joined by single spaces, so the text is (5 * count - 1) characters long and a greedy
    // wrap has somewhere to break.
    private static string Words(int count) => string.Join(' ', Enumerable.Repeat("word", count));

    private static JsonObject Reference(string pointer) => new() { ["$ref"] = pointer };

    private static JsonObject SampleSchema() => new()
    {
        ["properties"] = new JsonObject
        {
            ["args"] = new JsonObject { ["type"] = "array" },
            ["a/b"] = new JsonObject { ["type"] = "string" },
            ["c~d"] = new JsonObject { ["type"] = "boolean" },
        },
    };
}
