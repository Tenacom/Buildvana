# `FileBasedApps` module

This module adapts a file-based app to the repository that builds it.
It pins the `Buildvana.Runtime` package to the version of Buildvana SDK, and suppresses the two StyleCop rules such an app often cannot meet.
`bv`, Buildvana SDK, and the [hooks](../hooks.md) then agree on the shape of the configuration and of the hook args.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Configuration](#configuration)
- [Usage](#usage)
  - [`#:package` directive](#package-directive)
  - [Suppressed warnings](#suppressed-warnings)
  - [Release asset list](#release-asset-list)

---

## Configuration

The module has no property of its own, and Buildvana SDK imports it into every project.
It acts on a [file-based app](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/program-structure/file-based-programs) only, which [`BV_IsFileBasedAppProject`](../internal-use-properties.md#project-type) identifies.

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

### Suppressed warnings

A file-based app is one file, so every type it declares shares that file.
The file often has a name no type can match, as `post-update.cs`, the file of a hook.
The module adds SA1402, "File may only contain a single type", and SA1649, "File name should match first type name", to `NoWarn`.

### Release asset list

A file-based app is not part of the solution `bv pack` packs, so the [`ReleaseAssetList` module](release-asset-list.md#generatereleaseassetlist-property) writes no release asset list for it.
