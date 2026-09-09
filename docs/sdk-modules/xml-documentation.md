# `XmlDocumentation` module

This module generates the XML documentation file of a library project.
In a project that generates none, it turns the documentation warnings of the compiler and StyleCop off.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Configuration](#configuration)
  - [`GenerateDocumentationFile` property](#generatedocumentationfile-property)
- [Usage](#usage)
  - [Documentation file](#documentation-file)
  - [Suppressed warnings](#suppressed-warnings)
- [Diagnostics](#diagnostics)
- [Migrating from `XmlDocs`](#migrating-from-xmldocs)

---

## Configuration

### `GenerateDocumentationFile` property

[`GenerateDocumentationFile`](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#generatedocumentationfile) is the .NET SDK property that turns the XML documentation file on.
The module defaults it to `true` for a [library project](../internal-use-properties.md#project-type), and leaves every other project to the .NET SDK, which defaults it to `false`.
Set it in a project file or in `Common.props`, as in any .NET project.
A project that sets `DocumentationFile` and not `GenerateDocumentationFile` generates the file, as the .NET SDK provides.

---

## Usage

### Documentation file

When `GenerateDocumentationFile` is `true`, the compiler writes `$(AssemblyName).xml` to the intermediate output directory, and the .NET SDK copies it next to the assembly.
`dotnet pack` puts the file in the package next to the assembly, and `dotnet publish` copies it to the publish directory.
`DocumentationFile` sets the path of the file.

The compiler reports CS1591 for every public type and member without a documentation comment, and [StyleCop](standard-analyzers.md#usestylecopanalyzers-property) reports SA1600 with it.

### Suppressed warnings

When `GenerateDocumentationFile` is not `true`, the module adds the documentation warnings to `NoWarn`, after the ones the project lists.
They are CS1573, CS1591, and CS1712 of the compiler, and SA0001 and SA1600 to SA1629 of StyleCop.

---

## Diagnostics

The module raises no diagnostic in its [XmlDocumentation module (1800-1899)](../sdk-diagnostics.md#xmldocumentation-module-1800-1899) range.

---

## Migrating from `XmlDocs`

Buildvana SDK read `XmlDocs` in place of `GenerateDocumentationFile`, with the same defaults, and overwrote a `GenerateDocumentationFile` set by the project.
Replace every `XmlDocs` with `GenerateDocumentationFile`, in project files and in `Common.props`.
A project that set neither needs no change.
