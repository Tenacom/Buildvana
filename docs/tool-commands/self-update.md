# Self-update

`bv self-update` moves every Buildvana pin of the repository to one version: the version of the invoked `bv`, or the one `--to` names.
This page says what the command updates, what it prints, and when it refuses to run.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [What the command updates](#what-the-command-updates)
  - [Family pins](#family-pins)
- [The summary](#the-summary)
- [The invoked `bv` runs](#the-invoked-bv-runs)
- [Downgrades](#downgrades)
- [Options](#options)
- [Exit codes](#exit-codes)

---

## What the command updates

```text
bv self-update [OPTIONS]
```

`bv`, `Buildvana.Sdk`, and `Buildvana.Runtime` are released together, and a repository pins them at one version.
`bv self-update` moves every pin of the three to the target version, in this order:

1. The `bv` entry of the tool manifest, `.config/dotnet-tools.json`, through `dotnet tool update bv --version <target>`.
   When the manifest has no `bv` entry, `dotnet tool install bv --version <target> --create-manifest-if-needed` adds it, and creates the manifest when the repository has none.
   The .NET CLI downloads the version too, so the next `dotnet bv` runs it.
2. The `Buildvana.Sdk` entry under `msbuild-sdks` in `global.json`.
   `bv` creates the file, or the section, when it is missing, and keeps the formatting of the file otherwise.
3. Every pin of the three packages declared in the files of the repository.
   [Family pins](#family-pins) says which files and which pins.
4. The version segment of the `$schema` reference of `buildvana.jsonc`, when the reference has the form `https://raw.githubusercontent.com/Tenacom/Buildvana/<version>/schemas/buildvana.schema.json`.
   Any other reference is reported and left alone.

The manifest goes first, and always, even when it pins the target already.
The step is the one that runs a program, so a failure there leaves the repository untouched.
With `--to`, the step is also the existence check: a version no configured package source has fails the update before any file is written.

At the end, `bv` loads `buildvana.jsonc` with the model of the running `bv`, and reports every problem as a warning.
The file keeps working for the commands that do not read it, and you decide how to migrate it.

A manifest whose `bv` entry has no version, or an invalid one, stops the command at once, with a message naming the entry.
The .NET CLI cannot read such a manifest either, so the entry must be fixed by hand.

### Family pins

A family pin is a pin of `bv`, `Buildvana.Sdk`, or `Buildvana.Runtime` in a file of the repository.
`bv self-update` reads them from two kinds of file:

- MSBuild files, whose extension is `.props`, `.targets`, or ends in `proj`.
  The items read are `PackageVersion`, `GlobalPackageReference`, and `PackageReference`, plus the item name of every group `dependencies.additionalPackages` declares.
  A `VersionOverride` is never read or written.
- File-based apps, which are the `.cs` files under `.buildvana/hooks/` and the ones the `fileBasedApps` setting names.
  The pins are the `#:package` and `#:sdk` directives that carry a version.
  A directive without a version is a reference to a pin declared elsewhere, and is not a pin.

`bv` walks the home directory as Git does, so an ignored file never contributes a pin, and leaves the build output directories out as well.
The walk reads the files as text, without an MSBuild evaluation.
An evaluation would need the very SDK the update may be about to change.
`bv` splices the new version over the old one, byte for byte, and leaves the rest of the file as it was.
A pin whose version is not a literal, such as a property reference, a range, or a floating version, is reported and left alone.

The three packages form a closed list.
A third-party package whose id starts with `Buildvana.` never moves.

When `buildvana.jsonc` cannot be read, `bv` warns, searches file-based apps under `.buildvana/hooks/` alone, and reads the three built-in item names alone.
The command is the one that repairs a half-updated repository, so it runs on a configuration file that predates it.

---

## The summary

The summary is the result of the command, so it goes to standard output at every verbosity.
It holds one line per pin, in the order the pins were found, and a last line for the schema reference:

```text
bv: 2.1.500-preview -> 2.1.538-preview (tool manifest)
Buildvana.Sdk: 2.1.500-preview -> 2.1.538-preview (global.json)
Buildvana.Runtime: 2.1.538-preview (Directory.Packages.props, unchanged)
Buildvana.Sdk: $(BuildvanaVersion) (tools/Directory.Build.props, left alone)
buildvana.jsonc: schema reference updated
```

A line says `added` for a pin that did not exist, and `unchanged` for one at the target already.
It says `left alone` for a pin whose version is not a literal.
The last line says `schema reference updated`, `schema reference unchanged`, `schema reference not recognized, left unchanged`, or `no schema reference found`.
There is no last line when the repository has no `buildvana.jsonc`.

Every pin found appears, the unchanged ones included, so the summary doubles as a check that every intended pin was found.

---

## The invoked `bv` runs

[Delegation](../command-line.md#delegation) never applies to `bv self-update`.
The command runs the `bv` you invoke, and pins the repository to that version, so a delegated run would pin what is pinned already.

The usual upgrade takes two commands:

```shell
dotnet tool update -g bv
bv self-update
```

To pin a repository to an exact version without installing it, run the command through `dnx`:

```shell
dnx bv@2.1.538-preview self-update
```

To repair a repository whose pins disagree, run the command through the tool manifest, which pins every file to the version the manifest holds:

```shell
dotnet bv self-update
```

---

## Downgrades

`bv self-update` refuses to lower a pin.
When the tool manifest, `global.json`, or a family pin with a literal version is above the target, the command fails before touching anything.
The message names every such pin.
An old `bv` run by habit in a newer repository then never rolls it back.
`--force` allows the downgrade, for a deliberate one, such as bisecting a regression.
When the tool manifest pin is above the target, `bv` passes `--allow-downgrade` to `dotnet tool update`.
Without the flag, the .NET CLI refuses to lower a tool version.

---

## Options

| Option           | Meaning                                                                  |
| ---------------- | ------------------------------------------------------------------------ |
| `--force`        | Update the pins even when one is above the target, which is a downgrade. |
| `--to <VERSION>` | The version to pin. Defaults to the version of the invoked `bv`.         |

---

## Exit codes

`bv self-update` returns the [exit codes every `bv` command returns](../tool-diagnostics.md#exit-codes).

Code 1 is a refused downgrade, a manifest entry the command cannot use, or a file that cannot be read or written.
Code 2 is a refusal of the command line: a `--to` value that is not a version, or an option the command does not know.
Code 3 says that `dotnet tool update` or `dotnet tool install` failed, as when no source has the version.
Code 130 is a run terminated with Ctrl-C.
