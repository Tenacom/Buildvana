# Syntax of constants in ThisAssembly classes

The [`ThisAssemblyClass` module](sdk-modules/this-assembly-class.md) generates a `ThisAssembly` class from `ThisAssemblyConstant` items.
This page says how Buildvana SDK turns the `Value` metadata of an item into a typed constant.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Declaring constants](#declaring-constants)
- [Parsing steps](#parsing-steps)
- [Allowed types](#allowed-types)

---

## Declaring constants

A `ThisAssemblyConstant` item declares one constant.
The `Include` of the item is the name of the constant, and the `Value` metadata is its value.

```xml
<!-- Generation of a ThisAssembly class is disabled by default. -->
<PropertyGroup>
  <GenerateThisAssemblyClass>true</GenerateThisAssemblyClass>
</PropertyGroup>

<!-- Add an int constant named ThisAssembly.Answer with a value of 42. -->
<ItemGroup>
  <ThisAssemblyConstant Include="Answer" Value="42" />
</ItemGroup>
```

The `Value` may name the type of the constant, with one of the names that [Allowed types](#allowed-types) lists:

```xml
<ItemGroup>
  <ThisAssemblyConstant Include="Answer" Value="int:42" />
</ItemGroup>
```

---

## Parsing steps

Buildvana SDK reads the `Value` metadata and applies the first step below that matches it.

1. An empty `Value` yields a `string?` constant whose value is `null`.
2. A `Value` whose first and last characters are double quotes yields a `string` constant holding the text between them.
   To put a double quote inside the text, write it twice.
3. A `Value` holding a colon after its first character is read as `type:value`.
   `type` is one of the names that [Allowed types](#allowed-types) lists, and `value` is parsed as that type.
   When `type` is unknown, or `value` does not parse as that type, Buildvana SDK raises error BVSDK2301 and the build fails.
4. A `Value` that parses as an `int` yields an `int` constant.
5. A `Value` that parses as a `long` yields a `long` constant.
6. A `Value` that parses as a `bool` yields a `bool` constant.
7. Any other `Value` yields a `string` constant holding the text unchanged.

The table shows the constant each step produces, as the generated class declares it.

| `Value`                   | Step | Declaration                                           |
| ------------------------- | :--: | ----------------------------------------------------- |
| (empty)                   |  1   | `public const string? Name = null;`                   |
| `""`                      |  2   | `public const string Name = "";`                      |
| `"""Murder"", she wrote"` |  2   | `public const string Name = "\"Murder\", she wrote";` |
| `int:42`                  |  3   | `public const int Name = 42;`                         |
| `long:42`                 |  3   | `public const long Name = 42L;`                       |
| `42`                      |  4   | `public const int Name = 42;`                         |
| `-13`                     |  4   | `public const int Name = -13;`                        |
| `9999999999`              |  5   | `public const long Name = 9999999999L;`               |
| `true`                    |  6   | `public const bool Name = true;`                      |
| `foo`                     |  7   | `public const string Name = "foo";`                   |
| `false90`                 |  7   | `public const string Name = "false90";`               |
| `:foo`                    |  7   | `public const string Name = ":foo";`                  |

---

## Allowed types

The table lists the types a constant can have, with the names that select each one in the `type:value` form.
The names are case-insensitive.

| Type     | Names                            |
| -------- | -------------------------------- |
| `byte`   | `byte`, `uint8`, `System.Byte`   |
| `short`  | `short`, `int16`, `System.Int16` |
| `int`    | `int`, `int32`, `System.Int32`   |
| `long`   | `long`, `int64`, `System.Int64`  |
| `bool`   | `bool`, `System.Boolean`         |
| `string` | `string`, `System.String`        |
