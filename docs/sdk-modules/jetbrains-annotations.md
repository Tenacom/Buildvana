# `JetBrainsAnnotations` module

This module exports the [JetBrains annotations](https://www.jetbrains.com/help/resharper/Code_Analysis__Code_Annotations.html) of a C# project to a ReSharper external annotations file, and packs the file next to the assembly.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Configuration](#configuration)
  - [`ExportJetBrainsAnnotations` property](#exportjetbrainsannotations-property)
- [Usage](#usage)
  - [External annotations file](#external-annotations-file)
  - [Package content](#package-content)
- [Diagnostics](#diagnostics)
- [Migration to `ExportJetBrainsAnnotations`](#migration-to-exportjetbrainsannotations)

---

## Configuration

### `ExportJetBrainsAnnotations` property

Set it to `true` to export the annotations.
The default is `false`.
Buildvana SDK forces it to `false` in a project whose language is not C#, and in a project that uses the `Microsoft.Build.NoTargets` SDK, because the exporter reads C# source.

The module adds no package to the project.
Reference an annotations source yourself: the [`JetBrains.Annotations`](https://www.nuget.org/packages/JetBrains.Annotations) package, the [`JetBrains.Annotations.Sources`](https://www.nuget.org/packages/JetBrains.Annotations.Sources) package, or attributes of your own in the `JetBrains.Annotations` namespace.

---

## Usage

### External annotations file

After the build of each target framework, the `BV_ExportJetBrainsAnnotations` target runs the `ExportJetBrainsAnnotations` task.
The task builds a Roslyn compilation from the project's `Compile` items, its resolved references, `DefineConstants`, and `LangVersion`, and writes `$(AssemblyName).ExternalAnnotations.xml` to the output directory.
The target is incremental: it runs again when a source file or a reference changes.

The file describes the attributes of the `JetBrains.Annotations` namespace on the public, protected, and protected internal types and members of the assembly, on their parameters, and on their type parameters.
Six attributes that ReSharper does not read from an external annotations file are left out: `AspMvcSuppressViewError`, `LocalizationRequired`, `MeansImplicitUse`, `NoReorder`, `PublicAPI`, and `UsedImplicitly`.

When the attributes carry `[Conditional("JETBRAINS_ANNOTATIONS")]`, as the JetBrains packages define them, the compiled assembly holds no annotation metadata, and the file is the only carrier.

### Package content

At pack time, the `BV_IncludeJetBrainsAnnotationsInPackage` target adds the file of each target framework to the package, at `lib/<tfm>/`, next to the assembly.
Packing does not force a build, so a pack that follows a separate build step includes the file that build wrote.

---

## Diagnostics

The module raises the diagnostics of the [JetBrainsAnnotations module (1300-1399)](../sdk-diagnostics.md#jetbrainsannotations-module-1300-1399) range.

---

## Migration to `ExportJetBrainsAnnotations`

Releases before 2.1 exported annotations through the `UseJetBrainsAnnotations` property.
The module added the `JetBrains.Annotations` package to the project, read the annotations from the compiled assembly with Mono.Cecil in a second build pass, and supported Visual Basic projects.
Buildvana SDK ignores `UseJetBrainsAnnotations`.

To migrate a C# project:

1. Remove `UseJetBrainsAnnotations` from the project files.
2. Reference an annotations source, as [`ExportJetBrainsAnnotations` property](#exportjetbrainsannotations-property) describes.
3. Set `ExportJetBrainsAnnotations` to `true`.

A Visual Basic project cannot export annotations.
