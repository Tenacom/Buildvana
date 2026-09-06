# `ThisAssemblyClass` module

This module generates a static class, `ThisAssembly` by default, holding constants that describe the assembly being built.
The constants come from `ThisAssemblyConstant` items, from a default set Buildvana SDK defines, and from the [`Versioning` module](versioning.md).

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Configuration](#configuration)
  - [`GenerateThisAssemblyClass` property](#generatethisassemblyclass-property)
  - [`EnableDefaultThisAssemblyConstants` property](#enabledefaultthisassemblyconstants-property)
  - [`ThisAssemblyClassName` property](#thisassemblyclassname-property)
  - [`ThisAssemblyClassNamespace` property](#thisassemblyclassnamespace-property)
- [Usage](#usage)
  - [`ThisAssemblyConstant` items](#thisassemblyconstant-items)
  - [Generated class](#generated-class)
  - [Default constants](#default-constants)
- [Diagnostics](#diagnostics)

---

## Configuration

### `GenerateThisAssemblyClass` property

Set it to `true` to generate the class.
The default is `false`, except in a C# project where the `Versioning` module is active, as [`ThisAssembly` constants](versioning.md#thisassembly-constants) says.

The generator supports C# projects only.
A project in another language that sets the property to `true` gets warning BVSDK2300 and no class.
A project that uses the `Microsoft.Build.NoTargets` SDK gets no class and no warning.

### `EnableDefaultThisAssemblyConstants` property

Set it to `false` to leave the [default constants](#default-constants) out of the class, and the constants of the `Versioning` module with them.
The default is `true`.

### `ThisAssemblyClassName` property

The name of the generated class.
The default is `ThisAssembly`.

### `ThisAssemblyClassNamespace` property

The namespace of the generated class.
The default is empty, and the class then goes in the global namespace.

---

## Usage

### `ThisAssemblyConstant` items

An item declares one constant.
The name of the constant is the item's `Include`, and its value is the `Value` metadata.
[Syntax of constants in ThisAssembly classes](../constants-syntax.md) says how to write the value.
Declare the items outside any target.

```xml
<PropertyGroup>
  <GenerateThisAssemblyClass>true</GenerateThisAssemblyClass>
</PropertyGroup>

<ItemGroup>
  <ThisAssemblyConstant Include="Answer" Value="42" />
  <ThisAssemblyConstant Include="Greeting" Value="Hello, world!" />
</ItemGroup>
```

A `Value` that the syntax rejects raises error BVSDK2301.

### Generated class

The class is `internal`, `static`, and `partial`, and every constant is `public const`.
The project above gets this class, followed by the default constants:

```csharp
[global::System.Runtime.CompilerServices.CompilerGenerated]
[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal static partial class ThisAssembly
{
    public const int Answer = 42;
    public const string Greeting = "Hello, world!";
}
```

The class is partial, so a file of yours can add members to it.

A Roslyn source generator that Buildvana SDK ships writes the class, so the [toolchain floors](../introduction.md#toolchain) apply.
The generator reads the constants from a file that the `BV_WriteThisAssemblyConstantsFile` target writes before compilation.
The target writes the file because some values, such as version numbers, exist only once the build has computed them.

### Default constants

Unless `EnableDefaultThisAssemblyConstants` is `false`, the class holds these constants, each of type `string`:

| Constant                       | Value                     |
| ------------------------------ | ------------------------- |
| `AssemblyVersion`              | `$(AssemblyVersion)`      |
| `AssemblyFileVersion`          | `$(FileVersion)`          |
| `AssemblyInformationalVersion` | `$(InformationalVersion)` |
| `AssemblyName`                 | `$(AssemblyName)`         |
| `AssemblyTitle`                | `$(AssemblyTitle)`        |
| `AssemblyDescription`          | `$(Description)`          |
| `AssemblyProduct`              | `$(Product)`              |
| `AssemblyCompany`              | `$(Company)`              |
| `AssemblyCopyright`            | `$(Copyright)`            |
| `AssemblyConfiguration`        | `$(Configuration)`        |
| `RootNamespace`                | `$(RootNamespace)`        |

The names follow the assembly attributes, not the properties the values come from.
Buildvana SDK reads the values after the `GetAssemblyVersion` and `AddSourceRevisionToInformationalVersion` targets of the .NET SDK have run.
A default constant replaces a `ThisAssemblyConstant` item of the same name.
The `Versioning` module adds [five more](versioning.md#thisassembly-constants) when it is active.

---

## Diagnostics

The module raises the diagnostics of the [ThisAssemblyClass module (2300-2399)](../sdk-diagnostics.md#thisassemblyclass-module-2300-2399) range.
