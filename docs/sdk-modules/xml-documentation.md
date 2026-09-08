# `XmlDocumentation` module

This module generates the XML documentation file of a library project.
In every other project, it turns the documentation warnings of the compiler and StyleCop off.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Configuration](#configuration)
  - [`XmlDocs` property](#xmldocs-property)
- [Usage](#usage)
  - [Documentation file](#documentation-file)
  - [Suppressed warnings](#suppressed-warnings)
- [Diagnostics](#diagnostics)

---

## Configuration

### `XmlDocs` property

Set it to `true` to generate the XML documentation file of a project, or to `false` to leave it out.
The default is `true` for a [library project](../internal-use-properties.md#project-type), and `false` for every other project.
Any value other than `true` counts as `false`.

The module sets `GenerateDocumentationFile` from `XmlDocs`, so set `XmlDocs` rather than `GenerateDocumentationFile`.
A library project that sets `GenerateDocumentationFile` to `false`, and not `XmlDocs`, gets `XmlDocs` at `false`.

---

## Usage

### Documentation file

When `XmlDocs` is `true`, the compiler writes `$(AssemblyName).xml` to the intermediate output directory, and the .NET SDK copies it next to the assembly.
`dotnet pack` puts the file in the package next to the assembly, and `dotnet publish` copies it to the publish directory.
`DocumentationFile` sets the path of the file when `XmlDocs` is `true`, and the module empties it otherwise.

The compiler reports CS1591 for every public type and member without a documentation comment, and [StyleCop](standard-analyzers.md#usestylecopanalyzers-property) reports SA1600 with it.

### Suppressed warnings

When `XmlDocs` is `false`, the module adds the documentation warnings to `NoWarn`, after the ones the project lists.
They are CS1573, CS1591, and CS1712 of the compiler, and SA0001 and SA1600 to SA1629 of StyleCop.
The module also empties `DocumentationFile` and sets `PublishDocumentationFile` to `false`.

---

## Diagnostics

The module raises no diagnostic in its [XmlDocumentation module (1800-1899)](../sdk-diagnostics.md#xmldocumentation-module-1800-1899) range.
