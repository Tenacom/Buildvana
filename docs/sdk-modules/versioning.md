# `Versioning` module

This module computes the version of every project from a `VERSION` file and the Git history of the repository.
It sets the version properties a build and a package read, and contributes version constants to the `ThisAssembly` class.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Configuration](#configuration)
  - [`UseVersioning` property](#useversioning-property)
  - [`VERSION` file](#version-file)
  - [`release.branches` setting](#releasebranches-setting)
  - [`versioning.prereleaseTag` setting](#versioningprereleasetag-setting)
  - [`versioning.assemblyVersionPrecision` setting](#versioningassemblyversionprecision-setting)
- [Usage](#usage)
  - [`GetBuildVersion` target](#getbuildversion-target)
  - [Patch number](#patch-number)
  - [Public releases](#public-releases)
  - [`ThisAssembly` constants](#thisassembly-constants)
- [Diagnostics](#diagnostics)
- [Migration from Nerdbank.GitVersioning](#migration-from-nerdbankgitversioning)

---

## Configuration

### `UseVersioning` property

Buildvana SDK activates the module when the home directory holds a `VERSION` file.
`UseVersioning` overrides that rule in both directions.
`false` leaves the module off with the file present.
`true` with no file raises error BVSDK2000.

When the module is off, Buildvana SDK still defines an empty `GetBuildVersion` target, so that a target of yours can depend on it in every project.

### `VERSION` file

The file is plain text, in the home directory, and holds one version specification in the form `MAJOR.MINOR[-[tag]]`:

```text
2.0-preview
```

- `MAJOR` and `MINOR` are decimal numbers, with no leading zero and no `v` prefix.
- A `-` after `MINOR` marks a prerelease line.
- The tag after the `-` is optional, made of ASCII letters and digits, and informational.
  The effective tag comes from `versioning.prereleaseTag`, described below.
- Trailing whitespace is accepted.
  Anything else after the specification is an error.

The file holds no patch number.
Buildvana SDK computes it from the Git history, as [Patch number](#patch-number) describes.
The [`VERSION`](../directory-structure.md#version) section of the directory structure page says where the file sits among the other files of a repository.

### `release.branches` setting

A list of regular expressions in `buildvana.jsonc`.
Buildvana SDK matches each one against the whole short name of the current branch, as if the pattern were wrapped in `^(?:` and `)$`.
A branch that matches at least one pattern produces public releases, as [Public releases](#public-releases) describes.
The default is an empty list, so no branch produces a public release.

```jsonc
{
  "release": {
    "branches": ["^main$", "^v\\d+\\.\\d+$"]
  }
}
```

### `versioning.prereleaseTag` setting

The prerelease tag of every version on a prerelease line, such as `preview`.
It must be a valid SemVer prerelease identifier.
When `VERSION` marks a prerelease line and the setting is absent, the build fails with a message naming the setting.

```jsonc
{
  "versioning": {
    "prereleaseTag": "preview"
  }
}
```

### `versioning.assemblyVersionPrecision` setting

How much of the computed version goes into `AssemblyVersion`.

| Value   | `AssemblyVersion`     |
| ------- | --------------------- |
| `major` | `MAJOR.0.0.0`         |
| `minor` | `MAJOR.MINOR.0.0`     |
| `build` | `MAJOR.MINOR.PATCH.0` |

The default is `major`.
A lower precision keeps the assembly version stable across releases.

---

## Usage

### `GetBuildVersion` target

The target runs before compilation, assembly attribute generation, packing, and restore.
It runs the `ComputeVersion` task and sets these properties:

| Property               | Value                                                                              |
| ---------------------- | ---------------------------------------------------------------------------------- |
| `Version`              | `MAJOR.MINOR.PATCH`, followed by `-tag` on a prerelease line                       |
| `PackageVersion`       | The same as `Version`                                                              |
| `AssemblyVersion`      | `Version` cut at the configured precision, with a fourth component of `0`          |
| `FileVersion`          | `MAJOR.MINOR.PATCH.0`                                                              |
| `InformationalVersion` | `Version`, followed by the short commit ID on a build that is not a public release |

The target overwrites a value the project sets for any of them.
The module also sets `IncludeSourceRevisionInInformationalVersion` to `false`, so that the .NET SDK does not append a second commit ID.

The task computes the version once per build process, and every project of the build reads the same result.
`bv release` and `bv version` compute the version with the same rules, without a build.

### Patch number

The patch number is the Git height of the version line.
Buildvana SDK walks the history from `HEAD`, and counts the commits whose committed `VERSION` file holds the same `MAJOR.MINOR` as the one being built.

- The commit that sets `MAJOR.MINOR` has height 1.
  Each later commit on the line adds 1.
- Across a merge, a commit takes the highest height among its parents, plus 1.
- A commit that changes only the prerelease marker or the tag stays on the line.
- A `VERSION` file that did not exist before counts as a change of `MAJOR.MINOR`.
  The commit that creates the file has height 1, whatever history precedes it.
- Height 0 means that `HEAD` is not on the version line: its `VERSION` is missing or holds another `MAJOR.MINOR`, or the repository has no commit.
  The usual cause is a `VERSION` changed in the working tree and not committed.
  A build gets that version, and `bv release` refuses to publish it, because a build of the tagged commit would compute a different one.

Buildvana SDK reads the repository through LibGit2Sharp and needs no `git` executable.
The walk sees the commits the clone has.
A shallow clone computes a lower height, so fetch the full history on a CI runner.
With `actions/checkout`, that is `fetch-depth: 0`.

### Public releases

A build is a public release when the current branch matches a pattern of `release.branches`.
On any other branch, and in detached `HEAD` state, the build is not a public release.

On a build that is not a public release, `InformationalVersion` carries the first ten characters of the commit ID, prefixed with `g`.
The ID joins the prerelease part of the version with a dot, or becomes the prerelease part when the version has none: `1.2.3-preview.g0123456789` on a prerelease line, `1.2.3-g0123456789` on a stable line.
A repository with no commit has no ID, and `InformationalVersion` is then `Version` alone.
The `IsPublicRelease` constant of `ThisAssembly` carries the same verdict.

### `ThisAssembly` constants

When the module is active in a C# project, `GenerateThisAssemblyClass` defaults to `true`, and the [`ThisAssemblyClass` module](this-assembly-class.md) generates the class.
The module adds five constants to it, unless `EnableDefaultThisAssemblyConstants` is `false`:

| Constant          | Type     | Value                                     |
| ----------------- | -------- | ----------------------------------------- |
| `SimpleVersion`   | `string` | `MAJOR.MINOR.PATCH`                       |
| `SemVer`          | `string` | The same as `Version`                     |
| `IsPublicRelease` | `bool`   | Whether the build is a public release     |
| `IsPrerelease`    | `bool`   | Whether `VERSION` marks a prerelease line |
| `GitCommitId`     | `string` | The full commit ID of `HEAD`              |

A `ThisAssemblyConstant` item with one of these names is replaced.

---

## Diagnostics

The module raises the diagnostics of the [Versioning module (2000-2099)](../sdk-diagnostics.md#versioning-module-2000-2099) range.

---

## Migration from Nerdbank.GitVersioning

Releases before 2.1 versioned projects through Nerdbank.GitVersioning: a `version.json` file, the `NerdbankGitVersioning` module, and the `nbgv` tool.
The module added the `Nerdbank.GitVersioning` package to every project.
Buildvana SDK adds no versioning package, and [handles versioning itself](#getbuildversion-target).
A repository coming from that setup migrates in one commit:

1. Create `VERSION` in the home directory, holding the `version` value of `version.json`, such as `2.0-preview`.
   The Git height restarts at the commit that creates the file.
   When the latest published patch number is high, bump `MAJOR.MINOR` in the same commit.
   On the old line, every computed version stays below the published ones until the line holds more commits than the highest patch number published, and `bv release` refuses a version below the latest release tag.
2. Move `publicReleaseRefSpec` to `release.branches` in `buildvana.jsonc`.
   A refspec pattern becomes a pattern on the short branch name: `^refs/heads/main$` becomes `^main$`.
3. Move `release.firstUnstableTag` to `versioning.prereleaseTag`, and `assemblyVersion.precision` to `versioning.assemblyVersionPrecision`.
4. Delete `version.json`, and remove `nbgv` from `.config/dotnet-tools.json` when it is there.
5. Replace `UseNerdbankGitVersioning` with `UseVersioning` in project files.

Nerdbank.GitVersioning features with no counterpart:

- `pathFilters`: the height counts every commit of the version line.
- Nested `version.json` files: `VERSION` lives in the home directory only.
- Package version schemes: the package version is always SemVer 2.0.
- The `GitCommitDate`, `GitCommitAuthorDate`, `PublicKey`, and `PublicKeyToken` constants of `ThisAssembly`.

The `AssemblyDescription`, `SimpleVersion`, and `SemVer` constants of `ThisAssembly` have no Nerdbank.GitVersioning counterpart.
Buildvana SDK keeps the `GetBuildVersion` target name, so that a target depending on it still runs.
