# `SourceGenerators` module

This module adds the Roslyn source generators that Buildvana SDK ships to a C# project, and checks that the compiler can run them.
The [`AdditionalAssemblyInfo` module](additional-assembly-info.md) and the [`ThisAssemblyClass` module](this-assembly-class.md) generate their source through it.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Configuration](#configuration)
- [Usage](#usage)
  - [Package reference](#package-reference)
  - [Roslyn version check](#roslyn-version-check)
- [Diagnostics](#diagnostics)

---

## Configuration

The module has no property of its own.
It is active when the `AdditionalAssemblyInfo` module or the `ThisAssemblyClass` module generates source, and inactive otherwise.
The [`GenerateAdditionalAssemblyInfo`](additional-assembly-info.md#generateadditionalassemblyinfo-property) and [`GenerateThisAssemblyClass`](this-assembly-class.md#generatethisassemblyclass-property) properties turn the two modules on, and setting both to `false` keeps this module out of a project.

---

## Usage

### Package reference

Buildvana SDK ships the generators in the `analyzers` folder of its own package, `Buildvana.Sdk`.
The module adds a `PackageReference` to the package, at the version of Buildvana SDK, with `PrivateAssets="all"` and `IncludeAssets="analyzers"`.
`IncludeAssets="analyzers"` brings the generators into the compilation, and nothing else from the package.
`PrivateAssets="all"` keeps the reference private.
A project that references this one, and a NuGet package this one produces, get no dependency on `Buildvana.Sdk`.
`dotnet list package` shows the reference as auto-referenced, with the `(A)` mark.
A `PackageReference` of yours to `Buildvana.Sdk` replaces the module's reference, and under central package management a `PackageVersion` item sets its version.

### Roslyn version check

The generators need the compiler APIs of a minimum Roslyn version, and an older compiler cannot run them.
Before compilation, the module checks that the compiler meets the minimum version, and a project below it gets error BVSDK1101.
The message of BVSDK1101 names the minimum version, and the .NET SDK and Visual Studio versions that ship it.
The [toolchain](../introduction.md#toolchain) table of the introduction lists them too.
The check also raises error BVSDK1100 when the project is not C#.
A build never reaches it, because the `AdditionalAssemblyInfo` and `ThisAssemblyClass` modules turn themselves off outside C#.

---

## Diagnostics

The module raises the diagnostics of the [Source generators (1100-1199)](../sdk-diagnostics.md#source-generators-1100-1199) range.
