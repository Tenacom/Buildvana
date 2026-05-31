// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Buildvana.Sdk.Internal;

/// <summary>
/// Reconstructs a Roslyn compilation from a project's source and references, then emits a ReSharper
/// external-annotations XML document describing the <c>JetBrains.Annotations</c> attributes applied to its
/// externally-visible API. This is host-agnostic: it takes plain paths and returns an <see cref="XDocument"/>.
/// </summary>
internal static class JetBrainsAnnotationsExporter
{
    private const string JetBrainsAnnotationsNamespace = "JetBrains.Annotations";

    // Attributes that ReSharper does not read from external-annotation files.
    private static readonly string[] NonExportableAttributeNames =
    [
        "AspMvcSuppressViewErrorAttribute",
        "LocalizationRequiredAttribute",
        "MeansImplicitUseAttribute",
        "NoReorderAttribute",
        "PublicAPIAttribute",
        "UsedImplicitlyAttribute",
    ];

    public static XDocument Export(
        string assemblyName,
        IReadOnlyList<string> compileFilePaths,
        IReadOnlyList<string> referencePaths,
        IEnumerable<string> preprocessorSymbols,
        string? languageVersion)
    {
        var compilation = CreateCompilation(assemblyName, compileFilePaths, referencePaths, preprocessorSymbols, languageVersion);
        var root = new XElement("assembly", new XAttribute("name", assemblyName));
        var types = GetSourceTypes(compilation.Assembly.GlobalNamespace).Where(IsExternallyVisible);
        foreach (var type in types)
        {
            foreach (var member in TypeToXml(type))
            {
                root.Add(member);
            }
        }

        return new XDocument(root);
    }

    private static CSharpCompilation CreateCompilation(
        string assemblyName,
        IReadOnlyList<string> compileFilePaths,
        IReadOnlyList<string> referencePaths,
        IEnumerable<string> preprocessorSymbols,
        string? languageVersion)
    {
        var parseOptions = CreateParseOptions(preprocessorSymbols, languageVersion);
        var syntaxTrees = compileFilePaths.Select(path => ParseFile(path, parseOptions)).ToList();
        var references = referencePaths
            .Where(File.Exists)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true);
        return CSharpCompilation.Create(assemblyName, syntaxTrees, references, options);
    }

    private static CSharpParseOptions CreateParseOptions(IEnumerable<string> preprocessorSymbols, string? languageVersion)
    {
        var version = LanguageVersion.Default;
        if (!string.IsNullOrWhiteSpace(languageVersion))
        {
            // On failure, TryParse sets version to LanguageVersion.Default, which is exactly the fallback we want.
            _ = LanguageVersionFacts.TryParse(languageVersion, out version);
        }

        return new CSharpParseOptions(version, preprocessorSymbols: preprocessorSymbols);
    }

    private static SyntaxTree ParseFile(string path, CSharpParseOptions parseOptions)
    {
        using var stream = File.OpenRead(path);
        var text = SourceText.From(stream);
        return CSharpSyntaxTree.ParseText(text, parseOptions, path);
    }

    private static IEnumerable<INamedTypeSymbol> GetSourceTypes(INamespaceSymbol @namespace)
    {
        foreach (var type in @namespace.GetTypeMembers())
        {
            foreach (var typeOrNested in GetTypeAndNested(type))
            {
                yield return typeOrNested;
            }
        }

        foreach (var childNamespace in @namespace.GetNamespaceMembers())
        {
            foreach (var type in GetSourceTypes(childNamespace))
            {
                yield return type;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetTypeAndNested(INamedTypeSymbol type)
    {
        yield return type;
        foreach (var nested in type.GetTypeMembers())
        {
            foreach (var typeOrNested in GetTypeAndNested(nested))
            {
                yield return typeOrNested;
            }
        }
    }

    private static bool IsExternallyVisible(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (!IsExternallyVisible(current.DeclaredAccessibility))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsExternallyVisible(Accessibility accessibility)
        => accessibility
            is Accessibility.Public
            or Accessibility.Protected
            or Accessibility.ProtectedOrInternal;

    private static IEnumerable<XElement> TypeToXml(INamedTypeSymbol type)
    {
        var typeMember = BuildMember(type, [.. GetAnnotations(type), .. GetTypeParameters(type.TypeParameters)]);
        if (typeMember is not null)
        {
            yield return typeMember;
        }

        foreach (var member in type.GetMembers())
        {
            if (!IsExternallyVisible(member.DeclaredAccessibility))
            {
                continue;
            }

            var element = MemberToXml(member);
            if (element is not null)
            {
                yield return element;
            }
        }
    }

    private static XElement? MemberToXml(ISymbol member)
        => member switch
        {
            IMethodSymbol method => BuildMember(
                method,
                [.. GetAnnotations(method), .. GetTypeParameters(method.TypeParameters), .. GetParameters(method.Parameters)]),
            IPropertySymbol property => BuildMember(property, [.. GetAnnotations(property), .. GetParameters(property.Parameters)]),
            IFieldSymbol field => BuildMember(field, [.. GetAnnotations(field)]),
            IEventSymbol @event => BuildMember(@event, [.. GetAnnotations(@event)]),
            _ => null,
        };

    private static XElement? BuildMember(ISymbol symbol, IReadOnlyList<XElement> children)
    {
        if (children.Count == 0)
        {
            return null;
        }

        var id = symbol.GetDocumentationCommentId();
        return string.IsNullOrEmpty(id) ? null : new XElement("member", new XAttribute("name", id), children);
    }

    private static IEnumerable<XElement> GetParameters(ImmutableArray<IParameterSymbol> parameters)
        => parameters.Select(ParameterToXml).OfType<XElement>();

    private static XElement? ParameterToXml(IParameterSymbol parameter)
    {
        var annotations = GetAnnotations(parameter).ToList();
        return annotations.Count == 0
            ? null
            : new XElement("parameter", new XAttribute("name", parameter.Name), annotations);
    }

    private static IEnumerable<XElement> GetTypeParameters(ImmutableArray<ITypeParameterSymbol> typeParameters)
        => typeParameters.Select(TypeParameterToXml).OfType<XElement>();

    private static XElement? TypeParameterToXml(ITypeParameterSymbol typeParameter)
    {
        var annotations = GetAnnotations(typeParameter).ToList();
        return annotations.Count == 0
            ? null
            : new XElement("typeparameter", new XAttribute("name", typeParameter.Name), annotations);
    }

    private static IEnumerable<XElement> GetAnnotations(ISymbol symbol)
        => symbol.GetAttributes().Where(IsExportableAnnotation).Select(AttributeToXml);

    private static bool IsExportableAnnotation(AttributeData attribute)
    {
        var attributeClass = attribute.AttributeClass;
        if (attributeClass is null)
        {
            return false;
        }

        var containingNamespace = attributeClass.ContainingNamespace?.ToDisplayString();
        var isJetBrainsAnnotation = string.Equals(containingNamespace, JetBrainsAnnotationsNamespace, StringComparison.Ordinal);
        return isJetBrainsAnnotation && !NonExportableAttributeNames.Contains(attributeClass.Name, StringComparer.Ordinal);
    }

    private static XElement AttributeToXml(AttributeData attribute)
    {
        var ctorId = attribute.AttributeConstructor?.GetDocumentationCommentId() ?? string.Empty;
        var element = new XElement("attribute", new XAttribute("ctor", ctorId));
        foreach (var argument in attribute.ConstructorArguments)
        {
            element.Add(new XElement("argument", FormatArgument(argument)));
        }

        return element;
    }

    private static string FormatArgument(TypedConstant argument)
    {
        // JetBrains annotation constructors take primitives, strings, enums, and (rarely) System.Type;
        // arrays don't appear in their constructors, so an empty placeholder is acceptable.
        if (argument.Kind == TypedConstantKind.Array)
        {
            return string.Empty;
        }

        if (argument.Kind == TypedConstantKind.Type)
        {
            return (argument.Value as ITypeSymbol)?.ToDisplayString() ?? string.Empty;
        }

        return argument.Value switch
        {
            null => string.Empty,
            bool boolean => boolean ? bool.TrueString : bool.FalseString,
            string text => text,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            var other => other.ToString() ?? string.Empty,
        };
    }
}
