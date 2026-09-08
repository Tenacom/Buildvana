# `Hooks` module

This module pins the `Buildvana.Runtime` package to the version of Buildvana SDK, in every file-based app the repository builds.
`bv`, Buildvana SDK, and the [hooks](../hooks.md) then agree on the shape of the configuration and of the hook args.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Configuration](#configuration)
- [Usage](#usage)
  - [`#:package` directive](#package-directive)

---

## Configuration

The module has no property of its own, and Buildvana SDK imports it into every project.
It acts on [file-based apps](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/program-structure/file-based-programs) only.

---

## Usage

### `#:package` directive

Reference `Buildvana.Runtime` from a hook, or from any other file-based app of the repository, with a directive that states no version:

```csharp
#:package Buildvana.Runtime
```

The .NET SDK turns the directive into a `PackageReference` item, and the module pins the package to the version of Buildvana SDK.
Under central package management, the module adds a `PackageVersion` item for the package, unless the repository declares one.
Without central package management, the module writes the version into the `PackageReference` item, unless the directive states one.

Under central package management, a directive that states a version fails with NU1008, as any `PackageReference` item with a version does.
To pin another version there, declare a `PackageVersion` item.
