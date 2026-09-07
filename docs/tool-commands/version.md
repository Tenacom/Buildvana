# Version

`bv version show` reports the version of the repository, and `bv version advance` changes the version specification in `VERSION`.
This page says what each subcommand reports or writes, and how the analysis of a version spec change works.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [`bv version show`](#bv-version-show)
- [`bv version advance`](#bv-version-advance)
  - [The analysis](#the-analysis)
- [Options](#options)
- [Exit codes](#exit-codes)

---

## `bv version show`

```text
bv version show [OPTIONS]
bv version [OPTIONS]
```

`show` is the default subcommand, so `bv version` is a complete invocation.
The report has six lines:

```text
Current version:       2.1.539-preview
Latest version:        2.1.538-preview
Latest stable version: 1.1.10
Public release:        yes
Prerelease:            yes
Current branch:        main
```

- The current version is the version a build would get.
  `bv` computes it from `VERSION` and the Git height as the [`Versioning` module](../sdk-modules/versioning.md#getbuildversion-target) does, without a build.
- The latest version is the version of the first tag, walking the history from `HEAD`, whose name is a semantic version.
- The latest stable version is the version of the first such tag with no prerelease part.
  Both lines read `(none)` when there is no such tag.
- The public release line says whether the current branch [produces public releases](../sdk-modules/versioning.md#public-releases).
- The prerelease line says whether `VERSION` marks a prerelease line.
- The current branch is the short name of the branch, or `(detached HEAD)`.

The report is the result of the command, so it goes to standard output at every verbosity, and the narration goes to standard error.
`bv version show | some-parser` therefore receives the report alone.
The command needs no network, and runs no SDK version check.

---

## `bv version advance`

```text
bv version advance [CHANGE] [OPTIONS]
```

`advance` applies a change to the `MAJOR.MINOR[-[tag]]` specification in [`VERSION`](../sdk-modules/versioning.md#version-file).
`CHANGE` is one of five values, and `none` is the default:

| `CHANGE`   | Effect                                                              |
| ---------- | ------------------------------------------------------------------- |
| `none`     | No change.                                                          |
| `unstable` | Add the prerelease marker.                                          |
| `stable`   | Remove the prerelease marker.                                       |
| `minor`    | Advance `MINOR`, and start the line as a prerelease.                |
| `major`    | Advance `MAJOR`, reset `MINOR`, and start the line as a prerelease. |

The command runs the request through [the analysis](#the-analysis), and applies the change the analysis returns.
`--force` skips the analysis, and applies `CHANGE` as it is.

When the specification changes, `bv` writes `VERSION` with the tag `versioning.prereleaseTag` names, and prints two notices:

```text
notice: Version spec changed from 2.1-preview to 2.2-preview.
notice: Review and commit the modified VERSION file.
```

Otherwise it prints `notice: Version spec not changed.`

The file is left uncommitted, for review.
Until the commit, the new version line has a [Git height](../sdk-modules/versioning.md#patch-number) of 0, and a build gets `MAJOR.MINOR.0`.
`bv release` refuses to publish that version, as [The release commit](release.md#the-release-commit) says, and the commit gives the line height 1.

### The analysis

`bv version advance` and `bv release` compute the change to apply from the request and from the public API files, in five steps.
An increment is one of `none`, `minor`, and `major`.

1. The current increment says how far the current version is already above the latest stable version.
   It is `major` when `MAJOR` is greater, `minor` when `MINOR` is greater, and `none` otherwise.
   Without a stable version, it is `none`.
2. The required increment is what Semantic Versioning asks for the changes in the `PublicAPI.Unshipped.txt` files of the repository.
   A removed API, written as a line starting with `*REMOVED*`, requires `major`.
   An added API requires `minor`, and no change requires `none`.
   When the latest stable version has `MAJOR` 0, a removed API requires `minor`, and an added API requires `none`.
   With `--check-public-api false`, or `release.checkPublicApi` set to `false`, the required increment is `none`.
3. The requested increment is `major` for a `major` request, `minor` for a `minor` request, and `none` otherwise.
   When the required increment is greater, it replaces the requested one.
4. The actual increment is the requested one when it is greater than the current increment, and `none` otherwise.
   An increment the current version already carries is not applied twice.
5. The change to apply is `major` or `minor` when the actual increment says so.
   Otherwise it is the request, where a `major` or `minor` request becomes `none`.

At `normal` verbosity and above, `bv` prints each step as an `info:` line.

---

## Options

| Subcommand | Option                      | Meaning                                                                                       |
| ---------- | --------------------------- | --------------------------------------------------------------------------------------------- |
| `advance`  | `--check-public-api <BOOL>` | Whether the public API files take part in the analysis. Defaults to `release.checkPublicApi`. |
| `advance`  | `--force`                   | Apply `CHANGE` as it is, without the analysis.                                                |

`bv version show` has no option of its own.

---

## Exit codes

The two subcommands return the [exit codes every `bv` command returns](../tool-diagnostics.md#exit-codes).

Code 1 is a repository the command cannot work with.
The causes are a missing or invalid `VERSION`, a prerelease line with no `versioning.prereleaseTag`, no Git repository, and an invalid pattern in `release.branches`.
Code 2 is a refusal of the command line: an unknown `CHANGE` value, an argument too many, or an unknown option.
Code 130 is a run terminated with Ctrl-C.
