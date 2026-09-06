# Syntax of constants in ThisAssembly classes

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Overview](#overview)
- [How Buildvana SDK parses constant values](#how-buildvana-sdk-parses-constant-values)
- [Allowed types](#allowed-types)

## Overview

Constants in `ThisAssembly` classes are specified via `ThisAssemblyConstant` items:

```XML
  <!-- Generation of a ThisAssembly class is disabled by default. -->
  <PropertyGroup>
    <GenerateThisAssemblyClass>true</GenerateThisAssemblyClass>
  </PropertyGroup>

  <!-- Add a System.Int32 constant named ThisAssembly.Answer with a value of 42. -->
  <ItemGroup>
    <ThisAssemblyConstant Include="Answer" Value="42" />
  </ItemGroup>
```

The type of a constant may also be explicitly specified:

```XML
  <ItemGroup>
    <ThisAssemblyConstant Include="Answer" Value="int:42" />
  </ItemGroup>
```

> **NOTE:** `ThisAssembly` class generation is only supported in C# projects.

## How Buildvana SDK parses constant values

Given the `Value` metadata of a `ThisAssemblyConstant` item, Buildvana SDK performs the following steps:

- If the metadata is empty, the resulting constant is a null string (`public const string? Name = null;`).
- If the first and last characters of the metadata are double quotes, the result is a `System.String` whose value is the string between the double quotes. In this case, _double quote characters within the metadata must be doubled._  
  **Examples:** `""` -> the empty string; `"""Murder"", she wrote"` -> `"Murder", she wrote`.
- If the metadata contains a colon, it is assumed to be of the form `type:value`, where `type` must be one of the strings listed in the table [below](#allowed-types), and `value` must be parsable as the specified type. If `type` is not recognized, or `value` cannot be successfully parsed, an error is logged and the build stops.  
  **Examples:** `int:42` -> `42`; `long:42` -> `42L`.
- If the metadata can be successfully parsed as a `System.Int32`, the result is the parsed value.  
  **Examples:** `42` -> `42`; `-13` -> `-13`.
- If the metadata can be successfully parsed as a `System.Int64`, the result is the parsed value.  
  **Examples:** `9999999999` -> `9999999999L`; `-9999999999` -> `-9999999999L`.
- If the metadata can be successfully parsed as a `System.Boolean`, the result is the parsed value.  
  **Examples:** `true` -> `true`; `false` -> `false`.
- If none of the previous steps yields a result, the result is a `System.String` whose value is the metadata, unchanged.  
  **Examples:** `foo` -> `"foo"`; `false90` -> `"false90"`.

## Allowed types

The following table lists the recognized types for constants, along with the prefixes that select each of them in the `type:value` syntax.

| Type           | Recognized prefixes (case-insensitive) |
| -------------- | -------------------------------------- |
| System.Byte    | `System.Byte`, `byte`, `uint8`         |
| System.Int16   | `System.Int16`, `short`, `int16`       |
| System.Int32   | `System.Int32`, `int`, `int32`         |
| System.Int64   | `System.Int64`, `long`, `int64`        |
| System.Boolean | `System.Boolean`, `bool`               |
| System.String  | `System.String`, `string`              |
