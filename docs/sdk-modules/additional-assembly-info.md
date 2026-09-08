# `AdditionalAssemblyInfo` module

This module generates the `CLSCompliant` and `ComVisible` assembly attributes of a C# project from the MSBuild properties of the same names.
The .NET SDK generates neither attribute.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Configuration](#configuration)
  - [`GenerateAdditionalAssemblyInfo` property](#generateadditionalassemblyinfo-property)
  - [`GenerateAssemblyCLSCompliantAttribute` property](#generateassemblyclscompliantattribute-property)
  - [`CLSCompliant` property](#clscompliant-property)
  - [`GenerateAssemblyComVisibleAttribute` property](#generateassemblycomvisibleattribute-property)
  - [`ComVisible` property](#comvisible-property)
- [Usage](#usage)
  - [Generated attributes](#generated-attributes)
- [Diagnostics](#diagnostics)

---

## Configuration

### `GenerateAdditionalAssemblyInfo` property

Set it to `false` to turn the module off.
The default is `true`.

The generator supports C# projects only.
A project in another language that sets the property to `true` gets warning BVSDK1400 and no attributes.
A project that uses the `Microsoft.Build.NoTargets` SDK gets no attributes and no warning.

### `GenerateAssemblyCLSCompliantAttribute` property

Set it to `false` to leave the `CLSCompliant` attribute out.
The default is `true`.

### `CLSCompliant` property

The value of the `CLSCompliant` attribute.
The default is `true`.
Any value other than `true` counts as `false`.

### `GenerateAssemblyComVisibleAttribute` property

Set it to `false` to leave the `ComVisible` attribute out.
The default is `true`.

### `ComVisible` property

The value of the `ComVisible` attribute.
The default is `false`.
Any value other than `true` counts as `false`.

---

## Usage

### Generated attributes

With the defaults, the module adds this source to the compilation:

```csharp
[assembly:System.CLSCompliant(true)]
[assembly:System.Runtime.InteropServices.ComVisible(false)]
```

A Roslyn source generator that Buildvana SDK ships writes the attributes, and the [`SourceGenerators` module](source-generators.md) adds it to the compilation.

A file of the project that declares either attribute duplicates the generated one, and the compiler reports error CS0579.
Remove the attribute from the file, or set `GenerateAssemblyCLSCompliantAttribute` or `GenerateAssemblyComVisibleAttribute` to `false`.

---

## Diagnostics

The module raises the diagnostics of the [AdditionalAssemblyInfo module (1400-1499)](../sdk-diagnostics.md#additionalassemblyinfo-module-1400-1499) range.
