# Syntax of constants in ThisAssembly classes and LiteralAssemblyInfo parameters

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Overview](#overview)
- [How Buildvana SDK parses constant and parameter values](#how-buildvana-sdk-parses-constant-and-parameter-values)
- [Allowed types](#allowed-types)

## Overview

Constants in ThisAssembly classes are specified via `ThisAssemblyConstant` items:

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

Parameters for literal assembly attributes are specified as follows:

```XML
  <!-- [assembly:SomeNamespace.NiceAttribute(2, true, null, answer: "yes", foo: @"bar+""baz""")] -->
  <ItemGroup>
    <LiteralAssemblyAttribute Include="SomeNamespace.Nice">
      <_Parameter1>2</_Parameter1>
      <_Parameter2>true</_Parameter2>
      <_Parameter3 />
      <answer>yes</answer>
      <foo>bar+"baz"</foo>
    </LiteralAssemblyAttribute>
  </ItemGroup>
```

The type of a constant or parameter may also be explicitly specified:

```XML
  <ItemGroup>
    <ThisAssemblyConstant Include="Answer" Value="int:42" />
  </ItemGroup>

<ItemGroup>
    <LiteralAssemblyAttribute Include="SomeNamespace.Nice">
      <_Parameter1>int:2</_Parameter1>
      <_Parameter2>bool:true</_Parameter2>
      <_Parameter3 />
      <answer>string:yes</answer>
      <foo>"bar+"""baz"""</foo>
    </LiteralAssemblyAttribute>
  </ItemGroup>
```

## How Buildvana SDK parses constant and parameter values

Given the `Value` metadata of a `ThisAssemblyConstant` item, or a metadata of a `LiteralAssemblyAttribute` item, Buildvana SDK performs the following steps:

- If the metadata is empty, the resulting object is `null` (`Nothing` in VB).  
  **Examples:** `<_Parameter1 />` -> `null`; `<_Parameter1></_Parameter1>` -> `null`.
- If the first and last characters of the metadata are double quotes, the result is a `System.String` whose value is the string between the double quotes. In this case, _double quote characters within the metadata must be doubled._  
  **Examples:** `<_Parameter1>""</_Parameter1>` -> `""` (the empty string); `<_Parameter1>"""Murder"", she wrote"</_Parameter1>` -> `"\"Murder\", she wrote"`.
- If the metadata contains a colon, it is assumed to be of the form `type:value`, where `type` must be one of the strings listed in the table [below](#allowed-types), and `value` must be parsable as the specified type. If `type` is not recognized, or `value` cannot be successfully parsed, an error is logged and the build stops.  
  **Examples:** `<_Parameter1>int:42</_Parameter1>` -> `42`; `<_Parameter1>long:42</_Parameter1>` -> `42L` (`42&` in VB).
- If the metadata can be successfully parsed as a `System.Int32`, the result is the parsed value.  
  **Examples:** `<_Parameter1>42</_Parameter1>` -> `42`; `<_Parameter1>-13</_Parameter1>` -> `-13`.
- If the metadata can be successfully parsed as a `System.Int64`, the result is the parsed value.  
  **Examples:** `<_Parameter1>12345678901234567890</_Parameter1>` -> `12345678901234567890L` (`12345678901234567890&` in VB); `<_Parameter1>-99998888777766665555</_Parameter1>` -> `-99998888777766665555L` (`-99998888777766665555&` in VB).
- If the metadata can be successfully parsed as a `System.Boolean`, the result is the parsed value.  
  **Examples:** `<_Parameter1>true</_Parameter1>` -> `true`; `<_Parameter1>false</_Parameter1>` -> `false`.
- If none of the previous steps yields a result, the result is a `System.String` whose value is the metadata, unchanged.  
  **Examples:** `<_Parameter1>foo</_Parameter1>` -> `"foo"`; `<_Parameter1>false90</_Parameter1>` -> `"false90"`.

## Allowed types

The following table lists the recognized types for constants and parameters, along with

| Type           | Recognized prefixes (case-insensitive)    |
| -------------- | ----------------------------------------- |
| System.UInt8   | `System.Byte`, `byte`, `uint8`            |
| System.Int16   | `System.Int16`, `short`, `int16`          |
| System.Int32   | `System.Int32`, `int`, `int32`, `Integer` |
| System.Int64   | `System.Int64`, `long`, `int64`           |
| System.Boolean | `System.Boolean`, `bool`, `Boolean`       |
| System.String  | `System.String`, `string`                 |
