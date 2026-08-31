# Dependency management

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Overview](#overview)
- [Scopes](#scopes)
  - [Selecting scopes](#selecting-scopes)
- [Update policies](#update-policies)
  - [Policy strings](#policy-strings)
  - [Where a policy comes from](#where-a-policy-comes-from)
- [What bv manages](#what-bv-manages)
  - [Buildvana's own packages](#buildvanas-own-packages)
  - [Additional package groups](#additional-package-groups)
  - [File-based apps](#file-based-apps)
- [`bv dependencies show`](#bv-dependencies-show)
- [Exit codes](#exit-codes)
- [What the SDK contributes](#what-the-sdk-contributes)

## Overview

`bv dependencies` inspects and updates the dependencies of a repository: the .NET SDK version, the MSBuild project SDKs, the .NET local tools, and the NuGet package pins. A _pin_ is an exact version recorded in one of the files it manages.

The canonical name is `bv dependencies`; `bv deps` is an alias, and help and error messages use the canonical name. `show` is the default subcommand, as it is for `bv version`, so `bv deps` is a complete invocation.

Today the command has one subcommand, `show`, which works offline. The subcommands that resolve versions against package sources and apply them (`update` and `prune`) are being written; this page grows with them.

## Scopes

Four _scopes_ divide the dependencies, one per kind and per file:

| Scope      | What it manages      | Where it lives                                                                 |
| ---------- | -------------------- | ------------------------------------------------------------------------------ |
| `netsdk`   | The .NET SDK version | `global.json`, in the `sdk` section                                            |
| `sdks`     | MSBuild project SDKs | `global.json`, in the `msbuild-sdks` section, and `#:sdk` directives           |
| `tools`    | .NET local tools     | `.config/dotnet-tools.json`                                                    |
| `packages` | NuGet package pins   | central package management files, project files, additional group files, and `#:package` directives |

Configuration decides which scopes are managed at all: a scope whose policy is `disable` is managed by nothing, listed by nothing, and no command-line option brings it back. By default all four are managed.

When a file a scope reads is absent, or states no pin, the scope simply has no pins. Nothing is created, and nothing fails.

### Selecting scopes

Two families of options restrict a single invocation:

- `--netsdk`, `--sdks`, `--tools`, `--packages` name the scopes to manage;
- `--no-netsdk`, `--no-sdks`, `--no-tools`, `--no-packages` name the scopes to leave out.

The two families do not mix: naming a scope to manage and another to leave out states the selection twice, and the two statements can disagree, so it is a usage error.

An option that names a scope configuration disables changes nothing, and says so. An option that leaves out such a scope says what is already the case, and says it silently.

## Update policies

An update policy answers one question: given the current version of a pin, how far may an automatic update move it?

Two vocabularies exist, because a .NET SDK version is not SemVer: its patch field encodes the feature band, so in `10.0.402` the feature band is 4 and the patch is 2.

| Package policy | Meaning                                                     |
| -------------- | ------------------------------------------------------------ |
| `disable`      | Never move this pin.                                        |
| `exact`        | Move within the same version, i.e. a prerelease to its own stable release. |
| `revision`     | Move within the same major, minor and patch.                |
| `patch`        | Move within the same major and minor.                       |
| `minor`        | Move within the same major.                                 |
| `major`        | Move to the latest version.                                 |

| .NET SDK policy | Meaning                                             |
| --------------- | ----------------------------------------------------- |
| `disable`       | Never move the baseline.                            |
| `patch`         | Move within the same feature band.                  |
| `feature`       | Move within the same major and minor.               |
| `minor`         | Move within the same major.                         |
| `major`         | Move to the latest release.                         |
| `lts`           | Move to the latest long-term support release.       |

### Policy strings

A policy is written as its lowercase name, optionally followed by `-`, which allows prerelease versions: `minor` takes stable versions only, `minor-` takes prereleases too, and `lts-` follows the release candidates of an upcoming LTS release.

Each position accepts the vocabulary of its own scope. `lts` under `dependencies.policies` is an error, and so is `exact` under `dependencies.scopes.netsdk`.

### Where a policy comes from

Every pin has a policy. It is the first of these that states one:

1. the `UpdatePolicy` metadata of the pin itself, which only package items can carry;
2. the first matching pattern of `dependencies.policies`;
3. the policy of the additional package group the pin belongs to;
4. the policy of the pin's scope, which defaults to `major` for `netsdk` and `minor` for the other three.

A pattern is matched against a whole package id, ignoring case, with `*` standing for any run of characters and every other character standing for itself. Patterns are tried in the order the configuration file states them, and the first match wins: order is the only ranking, so a leading `*` silences every pattern after it.

`bv dependencies show` reports the composed policy of every pin, which makes it the place to see what the ladder produced.

## What bv manages

A pin is managed when the file that declares it states one exact version, and that is the only form an automatic update moves. Everything else is reported and left exactly as it is, because adopting `bv dependencies` must not require rewriting a repository first:

- a version range (`[1.0,2.0)`), which decides for itself what resolves;
- a floating version (`1.*`), which resolves anew at every restore;
- one version in brackets (`[13.0.4]`), which the report suggests writing without them;
- a version the file does not state itself, because an MSBuild property holds it or a `PackageReference Update="..."` elsewhere applies it;
- a `VersionOverride`, which is central package management's way of departing from the central pin for one project.

Two kinds of item are never pins at all. A reference an SDK injects, marked `IsImplicitlyDefined`, belongs to that SDK. An item declared outside the [home directory](DirectoryStructure.md#home-directory) belongs to whoever owns the file; `bv` names it at `detail` verbosity and moves on.

A pin is what one file says about one id, so ten projects sharing one `Directory.Build.props` reference have one pin between them, while one file stating one id at two versions, one per target framework, has two.

### Buildvana's own packages

`bv`, `Buildvana.Sdk` and `Buildvana.Runtime` are released in lockstep and must stay in lockstep, so `bv dependencies` never sees them, in any scope. [`bv self-update`](DirectoryStructure.md#globaljson) is the command that moves them, all at once.

### Additional package groups

A repository may pin package versions in files of its own, under an item name of its own. Buildvana itself is the example: the packages its SDK injects into projects are pinned as `BV_PackageVersion` items in `src/Buildvana.Sdk/Sdk/PackageVersions.props`, a file no project of this repository imports. Such groups are declared in configuration:

```jsonc
"dependencies": {
  "additionalPackages": {
    "SDK package injections": {      // the caption naming the group in reports
      "files": "src/Buildvana.Sdk/Sdk/PackageVersions.props",
      "items": "BV_PackageVersion",
      "policy": "minor"              // optional; defaults to the packages scope policy
    }
  }
}
```

Each file is evaluated on its own, so conditions, properties and metadata mean there what they mean everywhere. An item an import brings in from outside the group's own glob belongs to whatever file declares it, and is not the group's. A file two groups match belongs to the first that names it.

### File-based apps

A file-based app states its dependencies in the `#:` directives of its leading block, and those are pins like any other: `#:package Serilog@4.0.0` belongs to the `packages` scope, `#:sdk Microsoft.Build.Traversal@4.1.0` to the `sdks` scope, and the `.cs` file that holds them is what an update would edit.

A versionless directive names no version, so it is no pin: it is a reference to a pin declared elsewhere.

Which `.cs` files are apps is the repository's own statement, through the `fileBasedApps` setting, which always includes the hooks directory.

## `bv dependencies show`

`show` lists the pins of every selected scope, with the policy governing each, and everything else that can be said without a network:

- the pins nothing can move, each with the reason;
- the pins that state a prerelease under a policy taking only stable versions, which no update moves and no update undoes;
- a `global.json` whose `sdk.allowPrerelease` disagrees with the `netsdk` policy, which is derived state an apply run will write.

Pins are grouped by the file that declares them, and an additional group's pins appear under its caption. A selected scope with no pins says that it has none.

The command works offline. The MSBuild evaluation it runs for the `packages` scope is local work, with the same preconditions as building at all. It always exits 0 when it completes: everything it reports is a finding, and what to do about it is the reader's call.

## Exit codes

| Code | Meaning                                                     |
| ---- | ------------------------------------------------------------ |
| 0    | The command completed.                                      |
| 1    | The repository is in a state the command cannot work with, or its configuration could not be read. |
| 2    | The command line names something `bv` does not know, or asks for something impossible. |
| 3    | A step could not complete: MSBuild failed, or a file could not be read. Warnings say which. |

## What the SDK contributes

Two things, both of which `bv` drives and neither of which changes an ordinary build:

- the target that dumps a project's evaluated package items, which `bv dependencies` runs over the solution to see the `packages` scope as a build sees it. Taking the pins from evaluation, rather than from the files, is what makes conditions, imports and layered central package management mean the same thing to `bv` as they do to a build;
- the import of the transitive override files that a forthcoming `bv dependencies update` generates.

Both are steered by [internal-use properties](InternalUseProperties.md#dependency-management) that `bv` passes on the command line.
