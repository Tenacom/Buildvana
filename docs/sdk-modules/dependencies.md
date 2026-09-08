# `Dependencies` module

This module is what Buildvana SDK [contributes to `bv dependencies`](../tool-commands/dependencies.md#what-buildvana-sdk-contributes).
It imports the transitive override files that an apply run of `bv dependencies update` writes.
It dumps the package items of a project to a file when `bv dependencies` asks for them.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Configuration](#configuration)
- [Usage](#usage)
  - [Transitive override files](#transitive-override-files)
  - [`BV_DumpPackagePins` target](#bv_dumppackagepins-target)

---

## Configuration

The module has no property of its own, and Buildvana SDK imports it into every project.
`bv dependencies` passes two [internal-use properties](../internal-use-properties.md#dependency-management) on the command line, `BV_PinDumpDirectory` and `BV_SuppressTransitiveOverrides`, and the module reads them.
A build sets neither property, so the module imports every transitive override file that exists, and nothing runs `BV_DumpPackagePins`.

---

## Usage

### Transitive override files

[Transitive overrides](../tool-commands/dependencies.md#transitive-overrides) lift the transitive dependencies of the repository out of the versions security advisories cover.
An apply run of `bv dependencies update` writes them to two kinds of file:

- `Directory.TransitiveOverrides.props`, in the home directory, holds one `PackageVersion` item per lifted package, for the projects under central package management.
- `<ProjectName>.TransitiveOverrides.props`, beside a project, holds one `PackageReference` item per package that project must lift, with `PrivateAssets="all"`.

The module imports each file that exists, and creates neither.
When [`BV_SuppressTransitiveOverrides`](../internal-use-properties.md#bv_suppresstransitiveoverrides) is `true`, it imports neither file.

### `BV_DumpPackagePins` target

The target writes the evaluated `PackageVersion`, `GlobalPackageReference`, and `PackageReference` items of the project to a file in the directory that [`BV_PinDumpDirectory`](../internal-use-properties.md#bv_pindumpdirectory) names.
`bv dependencies` sets the property, runs the target over the projects of the solution, and reads the files back.
No build target depends on `BV_DumpPackagePins`, so a build never runs it.
