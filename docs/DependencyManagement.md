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
- [`bv dependencies update`](#bv-dependencies-update)
  - [Where versions come from](#where-versions-come-from)
  - [What a run writes, and in what order](#what-a-run-writes-and-in-what-order)
  - [Naming the pins a run is about](#naming-the-pins-a-run-is-about)
  - [Stating a version outright](#stating-a-version-outright)
  - [The `deps/post-update` hook](#the-depspost-update-hook)
- [`bv dependencies prune`](#bv-dependencies-prune)
- [Transitive overrides](#transitive-overrides)
  - [What a run does](#what-a-run-does)
  - [What no override may lift](#what-no-override-may-lift)
- [Exit codes](#exit-codes)
- [What the SDK contributes](#what-the-sdk-contributes)

## Overview

`bv dependencies` inspects and updates the dependencies of a repository: the .NET SDK version, the MSBuild project SDKs, the .NET local tools, and the NuGet package pins. A _pin_ is an exact version recorded in one of the files it manages.

The canonical name is `bv dependencies`; `bv deps` is an alias, and help and error messages use the canonical name. `show` is the default subcommand, as it is for `bv version`, so `bv deps` is a complete invocation.

The command has three subcommands. `show` works offline and states what the repository says about itself. `update` resolves target versions against the package sources and applies them. `prune` removes the pins nothing references any more.

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

It ends with the [transitive overrides](#transitive-overrides) in effect, each under the generated file that states it. Those are the state the last apply run left behind. Whether they are still needed is a question a restore answers, and `show` runs none.

Pins are grouped by the file that declares them, and an additional group's pins appear under its caption. A selected scope with no pins says that it has none.

A pin takes one line, `Serilog 3.0.0 (minor)`, and a note about it takes another, indented under it. The report has no columns. At the eighty columns of a CI log, a column layout divides the width among the columns and breaks ids and versions across lines, and neither is readable in halves.

The command works offline. The MSBuild evaluation it runs for the `packages` scope is local work, with the same preconditions as building at all. It always exits 0 when it completes: everything it reports is a finding, and what to do about it is the reader's call.

## `bv dependencies update`

`update` moves every pin of every selected scope as far as its policy allows, and no further. Its report gives each pin the line the `show` report gives it, with an arrow after it: `Serilog 3.0.0 (minor) -> 3.1.0 (latest: 3.1.0, 4.0.0-preview.2)`. The arrow points at the version the pin moves to, or at the words that say why it moves nowhere: `up to date`, `disabled`, `not managed`, `not selected`, or `held`. The versions in parentheses are the latest stable and prerelease the sources have, which are what a deliberate pin edit starts from. A pin already at its target is counted and left out; `--check --all` lists those as well.

`--check` reports what would change and changes nothing, exiting 1 when anything would. That is the staleness gate for CI.

Every pin is resolved before anything is written, so a run either has a target for each pin it manages or changes nothing at all.

### Where versions come from

Package versions come from the repository's own package sources, read through NuGet's client libraries: the whole hierarchical configuration chain from the home directory upwards, every enabled source type, and package source mapping. What `bv` sees is therefore what a restore sees. Authenticated sources are reached with the machine's credential providers, non-interactively: `bv` never stops to ask, because a command that did would hang a CI run instead of failing it.

.NET SDK versions come from the official .NET release index, which is also what says whether a release is long-term support.

Two things stop a run, and both leave the repository untouched:

- a source that cannot answer. A resolution against the sources that happened to reply could only be wrong in silence, and an "up to date" report is a claim, not a guess;
- a pin naming a package, or a version, that no source has. That is the repository's own error — a mistyped id, a source missing from `nuget.config` — and one run reports every one of them, each naming the file that declares the pin. See [the BV12xx diagnostics](ToolDiagnostics.md#dependency-management-1200-1299).

A version some source knows and has delisted is not that error. Delisting often means the version is vulnerable, so moving away from it is the remedy: the pin is reported, and the update proceeds.

### What a run writes, and in what order

A run writes the `packages` scope, then `tools`, then `sdks`, and `global.json` last of all.

Package pins and project SDK pins are spliced in the file that declares them: only the version text changes, so formatting, comments, attribute order and encoding survive byte for byte. A pin declared twice at the same version, once per target framework, moves in both places, because MSBuild evaluated the two as one pin.

A tool is delegated to `dotnet tool update <id> --local --version <target>`, one tool at a time, which keeps the manifest and what is actually installed in the CLI's hands. `--all` is unusable here: for a tool pinned to a prerelease line it insists on the latest stable version, which is a downgrade, and then refuses to do it.

`global.json` goes last because a `global.json` naming an SDK that is not installed breaks every `dotnet` invocation after it: `rollForward` never rolls down to an older patch. `sdk.allowPrerelease` is written along with it, to say what the `netsdk` policy says, and added when the file states none.

### Naming the pins a run is about

Arguments name the pins a run is about, as package ids or as globs: `bv deps update Microsoft.CodeAnalysis.*` is about those pins alone. A filter that matches nothing is not an error; the report says that there was nothing to do.

The .NET SDK has no package id, so a run that names pins leaves the baseline alone. Passing `--netsdk` next to such an argument states the contradiction outright, and is a usage error.

### Stating a version outright

`--to <VERSION>` states the version the named pins must reach. It is an assisted manual edit, so it overrules the policy: it moves a pin whose policy is `disable`, it crosses a prerelease line, and it is the one move that may lower a pin. It does not go with `--check`, which writes nothing by definition.

Two forms exist:

- with one argument naming a package id, every pin of that id in the selected scopes takes the version. It is an error when no source has that version, and when the id has no pin `bv` manages — which is always the case for a Buildvana family package, whose pins `bv self-update` moves as one;
- with no argument and `netsdk` as the only selected scope, `global.json` takes the version. Any other selected scope alongside is a usage error.

### The `deps/post-update` hook

A repository that derives something from what it pins — a property naming a compiler version, a floor implied by a package — updates what it derives in the `deps/post-update` hook, which runs at the end of every `update` that ran to completion, check runs included. In a check run the hook's exit code 1 says that it would change something, and the command folds that into its own verdict. See [Hooks](Hooks.md#the-depspost-update-hook).

## `bv dependencies prune`

`prune` removes the central package pins nothing references any more. Such pins accumulate on their own: `dotnet package remove` deletes the reference and leaves the pin behind, and nothing else removes it.

A pin is _orphaned_ when no project of the solution references its package directly, and no versionless `#:package` directive names it. The verdict is NuGet's own. `prune` restores the solution and reads what each project's dependency graph states about its own direct references. A textual scan of the MSBuild files could not answer as much: a reference written through a property or an item transform is one only an evaluation sees. The restore leaves the [transitive override files](#transitive-overrides) out of the evaluation, so a promotion `bv` wrote never makes a pin look alive.

That restore is why the diagnosis lives here rather than in the offline `show`. Removing an entry and moving a version forward are different verbs with different consequences, which is why they are different commands.

Only central pins can be orphaned. A `PackageReference` is itself the reference, and so is a `#:package` directive that carries a version, so neither can outlive anything. The `netsdk`, `sdks`, and `tools` scopes are ignored in silence, and a run whose scope selection leaves out the `packages` scope does nothing and succeeds.

What makes a pin central is its item type, not the file that declares it: a `PackageVersion` item an [additional package group](#additional-package-groups) declares is a central pin, and is judged like one. A group whose items carry a name of its own, `BV_PackageVersion` say, declares no central pin, and `prune` passes over it.

Transitive pinning changes the question a project answers. A project with `CentralPackageTransitivePinningEnabled` set raises the version of a package it never references, from the central pin alone, so a direct reference is no longer what keeps a pin alive there. Such a project is judged by its whole resolved graph instead: a pin whose package the project resolves, at any depth, is in use. A pin whose package the graph never resolves raises nothing, and is an orphan there as anywhere else.

Policy plays no part. A policy says how far a pin may move, and says nothing about whether the repository still needs it, so a pin whose policy is `disable` is removed like any other.

`prune --check` reports the orphaned pins and removes none, exiting 1 when there is at least one. No argument names the pins a run is about: an orphan is a pin nothing references, and a filter that hid one would leave the repository stating a pin the same run had just called dead.

A removal can change what the [transitive overrides](#transitive-overrides) must say, because a promotion may rest on the pin that has just gone: a promoted reference whose central pin is missing is one the next restore rejects. A run that removed at least one pin therefore regenerates the override files before it ends. The `deps/post-update` hook then runs, as it does at the end of every `prune` that ran to completion, whether or not anything was removed. Every pin reaches it as skipped, `prune` having resolved none, and a pin the run removed does not reach it at all.

## Transitive overrides

A repository receives packages it never references: one package it does reference brings another, which brings a third. When a security advisory covers the version one of those ends up at, `bv dependencies update` lifts it. It writes a generated file forcing a higher version, and the Buildvana SDK imports the file.

Two kinds of file carry the overrides, and both belong to `bv`:

- `Directory.TransitiveOverrides.props`, at the repository root, states the versions for the projects that manage their package versions centrally;
- `<ProjectName>.TransitiveOverrides.props`, beside a project, promotes to a reference of that project each package the project must lift.

Do not edit either file. Every apply run that manages the `packages` scope rewrites both from scratch, which is also why a stale override needs no cleaning up: it is simply not written again. A promotion carries `PrivateAssets="all"`, so a package a project produces declares exactly the dependencies it would declare without the file.

Only vulnerability lifting is automatic. An override written for it is a lower bound, it has an oracle nobody has to read, and it disappears on its own once the package that brought the vulnerable version raises its own floor. Every other reason to force a transitive version — holding one back, unifying a version across the solution — has none of those properties. Write those as an ordinary reference with a pin, and let the pin's policy say what may move it.

### What a run does

The lifecycle runs at the end of an apply run, after the pins are written and before the hook. A check run leaves it alone entirely: predicting what a restore would find is not prediction but a restore.

1. Restore, with the override files left out of the evaluation. That is NuGet's verdict on the graph as the repository states it, which is what the overrides are computed from.
2. Read each project's dependency graph and take restore's own audit findings from it. `bv` does not re-implement the audit, so every NuGet audit setting is honored on its owner's terms: `NuGetAudit`, `NuGetAuditMode`, `NuGetAuditLevel`, `NuGetAuditSuppress`, `NoWarn`, and `<auditSources>`. What a project does not report is not lifted.
3. For each vulnerable package, take the lowest version its sources list that lies outside every advisory known for it and within its update policy, anchored at the version the project resolves. A package the repository already pins safely gets no version of its own: the project is promoted at the pin, and no new version appears anywhere.
4. Write the files, restore again with them in place, and repeat from step 2 until the graph stops changing. Promotion can itself change the graph, which is why the graph is read again. Each pass writes everything selected so far in the run, because the graph of a later pass no longer reports what an earlier one lifted.

The whole repository is judged, whatever the invocation named. A run aimed at one package can therefore end with a warning about another: the files describe the graph, not the command line.

### What no override may lift

Two things end a run with a warning that changes no exit code. The vulnerability stays, NuGetAudit keeps reporting it at every build, and what to do about it is the repository's call.

The first is a package no version can lift: none the sources list falls outside every advisory, or the only one that does lies beyond what the package's policy allows. Widening the policy for that package, through a `dependencies.policies` entry, is what lets the next run lift it.

The second is a package a decision of the repository's own governs. `bv` never introduces a version for a package the repository pins or references itself, so a vulnerable direct reference, a vulnerable central pin, and a central pin below the version a project resolves are all left alone. Move the pin, or suppress the advisory through NuGet's own `NuGetAuditSuppress`.

## Exit codes

The dependency commands return the [exit codes every `bv` command returns](ToolDiagnostics.md#exit-codes), with no meaning of their own added.

Code 1 is the verdict of `update --check`: a pin has fallen behind its policy, or the hook says it would change something. It is the verdict of `prune --check` as well, where it says that the repository states a pin nothing references. Nothing failed, and nothing was written. It is also the code of every error above that stops a run before it writes: a pin the sources do not know, a source that cannot be reached, a version `--to` names and no source has. One failure of its own carries it too: transitive overrides that never stop changing. No program failed there, and the procedure that gave up is `bv`'s own.

Code 2 is a refusal of the command line itself: scope options of both families at once, `--all` without `--check`, `--to` with `--check`, `--netsdk` next to an argument naming pins, `--to` naming the .NET SDK beside another scope, or a version that does not parse.

Code 3 is the one a reader of a report should know about: it says that a program `bv` ran failed, or answered with something `bv` cannot read. The pins of the `packages` scope come from an MSBuild evaluation, and a report missing that scope would otherwise read as a repository with no packages; a failed `dotnet tool update` and a failed hook are two more. The [override lifecycle](#transitive-overrides) adds two of its own: a restore that failed for a reason other than its own audit findings, and a restore that could not read a package source in full. The restore [`prune`](#bv-dependencies-prune) runs carries the first of the two, and not the second: what a project references does not depend on vulnerability data. Overrides regenerated from a fraction of the advisories would delete one that is still needed, so an incomplete answer stops the run and leaves every file as it stands.

## What the SDK contributes

Two things, both of which `bv` drives and neither of which changes an ordinary build:

- the target that dumps a project's evaluated package items, which `bv dependencies` runs over the solution to see the `packages` scope as a build sees it. Taking the pins from evaluation, rather than from the files, is what makes conditions, imports and layered central package management mean the same thing to `bv` as they do to a build. The dump also carries the two values the [override lifecycle](#transitive-overrides) needs from an evaluation: where a restore writes that project's dependency graph, and the severity the project's audit reports from;
- the import of the [transitive override files](#transitive-overrides), where they exist. A repository whose graph needs none has none, and the import finds nothing.

Both are steered by [internal-use properties](InternalUseProperties.md#dependency-management) that `bv` passes on the command line.
