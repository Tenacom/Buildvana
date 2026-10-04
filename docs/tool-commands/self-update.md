# Self-update

`bv self-update` moves every Buildvana pin of the repository to one version: the latest on the package sources, or the one `--preview`, `--repair`, or `--to` picks.
This page says what the command updates, which version it picks, what it prints, which `bv` runs, and when it refuses to run.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [What the command updates](#what-the-command-updates)
  - [Family pins](#family-pins)
- [The target version](#the-target-version)
- [The summary](#the-summary)
- [Which `bv` runs](#which-bv-runs)
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

1. The `bv` entry of the tool manifest, `dotnet-tools.json`, through `dotnet tool update bv --version <target> --tool-manifest dotnet-tools.json`.
   When the manifest has no `bv` entry, `dotnet tool install bv --version <target> --tool-manifest dotnet-tools.json` adds it.
   When the home directory has no manifest, `dotnet new tool-manifest` creates it there first.
   The .NET CLI downloads the version too, so the next `dotnet bv` runs it.
2. The `Buildvana.Sdk` entry under `msbuild-sdks` in `global.json`.
   `bv` creates the file, or the section, when it is missing, and keeps the formatting of the file otherwise.
3. Every pin of the three packages declared in the files of the repository.
   [Family pins](#family-pins) says which files and which pins.
4. The version segment of the `$schema` reference of `buildvana.jsonc`, when the reference has the form `https://raw.githubusercontent.com/Tenacom/Buildvana/<version>/schemas/buildvana.schema.json`.
   Any other reference is reported and left alone.

The manifest goes first, and always, even when it pins the target already.
The step is the one that runs a program, so a failure there leaves the repository untouched.
With `--repair` or `--to`, the step is also the existence check.
A version no configured package source has fails the update before any file is written.

At the end, `bv` loads `buildvana.jsonc` with the model of the target version, and reports every problem as a warning.
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

## The target version

The target version is the version every pin moves to.
`bv self-update` picks it from a reference version.
The reference is the `bv` version the tool manifest pins, or the version of the invoked `bv` when the manifest has no `bv` entry.
Without an option naming the target, the command reads the `bv` versions that [the package sources of the repository](dependencies.md#where-versions-come-from) list, and picks:

1. the latest stable version at or above the reference;
2. otherwise, when the reference is a prerelease, the latest prerelease at or above the reference;
3. otherwise, the reference itself, and the run aligns the other pins to it.

A stable pin therefore never moves to a prerelease by itself.
A prerelease pin moves to the first stable version that reaches it, whatever newer prereleases exist.
A repository pinned at 2.2.1-preview, with 2.2.7 and 2.3.1-preview on the sources, moves to 2.2.7.
Only a listed version is a candidate, because a delisted version is often a vulnerable one.
When no package source is configured, when no source knows `bv`, or when a source cannot answer, the command fails.
A source that knows nothing of `bv` is a misconfigured source, and a summary saying `unchanged` would hide it.

Three options name the target instead, and no two of them go together:

- `--preview` picks the latest listed version at or above the reference, prereleases included.
  The repository above moves to 2.3.1-preview.
- `--repair` picks the version the tool manifest pins, and asks no source.
  When the manifest has no `bv` entry, the command fails.
- `--to <VERSION>` picks the version given, and asks no source.

With `--repair` or `--to`, the `dotnet tool update` step is the existence check, as [What the command updates](#what-the-command-updates) says.

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

## Which `bv` runs

[Delegation](../command-line.md#delegation) never applies to `bv self-update`.
The invoked `bv` picks the target version and moves the tool manifest pin, whatever the manifest pinned before.
A delegated run would let the pinned `bv` pick the target, and an older pinned `bv` may not know the options of the invoked one.

When the target differs from the invoked `bv`, the invoked `bv` moves the manifest pin, then hands the rest of the run to the target.
It prints `Handing off to bv <target>, now pinned in this repository's tool manifest.` on standard error.
It then runs `dotnet tool run bv -- <global options> --nologo self-update --to <target>`, with `--force` when the run had it.
The handed-off run starts from the home directory, inherits the standard streams, updates the other pins, and prints [the summary](#the-summary).
Its exit code is the exit code of the command.
The target version knows its own configuration model and its own family pin rules, and the invoked `bv` may predate both.
The summary of the handed-off run reports the manifest move as `<previous> -> <target> (tool manifest)`, because the invoked `bv` passes the previous pin in [`BV_SELF_UPDATE_FROM`](../environment-variables.md#bv_self_update_from).

The usual update is one command, from the `bv` the manifest pins or from any other:

```shell
dotnet bv self-update
```

To move the repository to an exact version, name it:

```shell
dotnet bv self-update --to 2.1.538-preview
```

To repair a repository whose pins disagree, pin every file to the version the manifest holds:

```shell
dotnet bv self-update --repair
```

---

## Downgrades

`bv self-update` refuses to lower a pin.
When the tool manifest, `global.json`, or a family pin with a literal version is above the target, the command fails before touching anything.
The message says where the target came from, and names every such pin.
A `--to` or `--repair` run in a repository whose pins went past the target then never rolls it back.
Neither does a default run whose sources list nothing as new as the pins.
`--force` allows the downgrade, for a deliberate one, such as bisecting a regression, and crosses the handoff with `--to`.
When the tool manifest pin is above the target, `bv` passes `--allow-downgrade` to `dotnet tool update`.
Without the flag, the .NET CLI refuses to lower a tool version.

---

## Options

| Option           | Meaning                                                                                       |
| ---------------- | --------------------------------------------------------------------------------------------- |
| `--force`        | Update the pins even when one is above the target, which is a downgrade.                      |
| `--preview`      | Update to the latest version on the package sources, prereleases included.                    |
| `--repair`       | Update the repository to the `bv` version the tool manifest pins, without asking the sources. |
| `--to <VERSION>` | Update the repository to this version, without asking the sources.                            |

---

## Exit codes

`bv self-update` returns the [exit codes every `bv` command returns](../tool-diagnostics.md#exit-codes).

Code 1 is a refused downgrade, a manifest entry the command cannot use, or a file that cannot be read or written.
Code 1 also says that `--repair` found no `bv` entry, or that a package source is missing or cannot answer.
Code 2 is a refusal of the command line: a `--to` value that is not a version, or an option the command does not know.
Two of `--preview`, `--repair`, and `--to` together are code 2 as well.
Code 3 says that `dotnet tool update` or `dotnet tool install` failed, as when no source has the version.
Code 130 is a run terminated with Ctrl-C.
When the run hands off, the exit code is the one of the handed-off run.
