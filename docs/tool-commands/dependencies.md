# Dependency management

`bv dependencies` inspects and updates the dependencies of a repository.
Those are the .NET SDK version, the MSBuild project SDKs, the .NET local tools, and the NuGet package pins.
A _pin_ is an exact version recorded in one of the files the command manages.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Subcommands](#subcommands)
- [Scopes](#scopes)
  - [Selecting scopes](#selecting-scopes)
- [Update policies](#update-policies)
  - [Policy strings](#policy-strings)
  - [Where a policy comes from](#where-a-policy-comes-from)
- [What `bv` manages](#what-bv-manages)
  - [Buildvana's own packages](#buildvanas-own-packages)
  - [Additional package groups](#additional-package-groups)
  - [File-based apps](#file-based-apps)
- [`bv dependencies show`](#bv-dependencies-show)
- [`bv dependencies update`](#bv-dependencies-update)
  - [Where versions come from](#where-versions-come-from)
  - [What a run writes, and in what order](#what-a-run-writes-and-in-what-order)
  - [Naming the pins a run is about](#naming-the-pins-a-run-is-about)
  - [Moving a pin past its policy](#moving-a-pin-past-its-policy)
  - [The `deps/post-update` hook](#the-depspost-update-hook)
- [`bv dependencies prune`](#bv-dependencies-prune)
- [Transitive overrides](#transitive-overrides)
  - [What a run does](#what-a-run-does)
  - [What no override may lift](#what-no-override-may-lift)
- [What Buildvana SDK contributes](#what-buildvana-sdk-contributes)
- [Options](#options)
- [Exit codes](#exit-codes)

---

## Subcommands

`bv dependencies` is the canonical name, and `bv deps` is an alias.
Help and error messages use the canonical name.
`show` is the default subcommand, as it is for `bv version`, so `bv deps` is a complete invocation.

The command has three subcommands.
`show` works offline, and states what the repository says about itself.
`update` resolves target versions against the package sources, and applies them.
`prune` removes the pins nothing references any more.

Every subcommand runs [the SDK version check](../command-line.md#the-sdk-version-check) first, because the `packages` scope comes from an MSBuild evaluation under Buildvana SDK.

---

## Scopes

Four _scopes_ divide the dependencies, one per kind and per file:

| Scope      | What it manages      | Where it lives                                                                                      |
| ---------- | -------------------- | --------------------------------------------------------------------------------------------------- |
| `netsdk`   | The .NET SDK version | `global.json`, in the `sdk` section                                                                 |
| `sdks`     | MSBuild project SDKs | `global.json`, in the `msbuild-sdks` section, and `#:sdk` directives                                |
| `tools`    | .NET local tools     | `.config/dotnet-tools.json`                                                                         |
| `packages` | NuGet package pins   | central package management files, project files, additional group files, and `#:package` directives |

`buildvana.jsonc` decides which scopes are managed at all.
A scope whose policy is `disable` is managed by nothing, listed by nothing, and no command-line option brings it back.
By default, all four are managed.

When a file a scope reads is absent, or states no pin, the scope has no pins.
Nothing is created, and nothing fails.

### Selecting scopes

Two families of options restrict one invocation:

- `--netsdk`, `--sdks`, `--tools`, and `--packages` name the scopes to manage;
- `--no-netsdk`, `--no-sdks`, `--no-tools`, and `--no-packages` name the scopes to leave out.

The two families do not mix.
Naming a scope to manage and another to leave out states the selection twice.
The two statements can disagree, so the command reports a usage error.

An option that names a scope `buildvana.jsonc` disables changes nothing, and says so.
An option that leaves out such a scope says what is already the case, and says it silently.

---

## Update policies

An update policy says how far an automatic update may move a pin from its current version.

Two vocabularies exist, because a .NET SDK version is not SemVer.
Its patch field encodes the feature band, so in `10.0.402` the feature band is 4 and the patch is 2.

| Package policy | Meaning                                                                    |
| -------------- | -------------------------------------------------------------------------- |
| `disable`      | Never move this pin.                                                       |
| `exact`        | Move within the same version, i.e. a prerelease to its own stable release. |
| `revision`     | Move within the same major, minor and patch.                               |
| `patch`        | Move within the same major and minor.                                      |
| `minor`        | Move within the same major.                                                |
| `major`        | Move to the latest version.                                                |

| .NET SDK policy | Meaning                                       |
| --------------- | --------------------------------------------- |
| `disable`       | Never move the baseline.                      |
| `patch`         | Move within the same feature band.            |
| `feature`       | Move within the same major and minor.         |
| `minor`         | Move within the same major.                   |
| `major`         | Move to the latest release.                   |
| `lts`           | Move to the latest long-term support release. |

### Policy strings

A policy is written as its lowercase name, with an optional `-` after it, which allows prerelease versions.
`minor` takes stable versions only, `minor-` takes prereleases too, and `lts-` follows the release candidates of an upcoming LTS release.

Each position accepts the vocabulary of its own scope.
`lts` under `dependencies.policies` is an error, and so is `exact` under `dependencies.scopes.netsdk`.

### Where a policy comes from

Every pin has a policy.
It is the first of these that states one:

1. the `UpdatePolicy` metadata of the pin itself, which only a package item can carry;
2. the first matching pattern of `dependencies.policies`;
3. the policy of the additional package group the pin belongs to;
4. the policy of the scope of the pin, which defaults to `major` for `netsdk` and `minor` for the other three.

A pattern is matched against a whole package id, ignoring case.
`*` stands for any run of characters, and every other character stands for itself.
`bv` tries the patterns in the order `buildvana.jsonc` states them, and the first match wins.
Order is the only ranking, so a leading `*` silences every pattern after it.

`bv dependencies show` reports the composed policy of every pin, so it is the place to see what the four steps produced.

---

## What `bv` manages

A pin is managed when the file that declares it states one exact version, and that is the only form an automatic update moves.
Everything else is reported and left as it is, because adopting `bv dependencies` must not require rewriting a repository first:

- a version range, `[1.0,2.0)`, which decides for itself what resolves;
- a floating version, `1.*`, which resolves anew at every restore;
- one version in brackets, `[13.0.4]`, which the report suggests writing without them;
- a version the file does not state itself, because an MSBuild property holds it, or a `PackageReference Update="..."` elsewhere applies it;
- a `VersionOverride`, which is how central package management departs from the central pin for one project.

Two kinds of item are never pins at all.
A reference an SDK injects, marked `IsImplicitlyDefined`, belongs to that SDK.
An item declared outside the [home directory](../directory-structure.md#home-directory) belongs to whoever owns the file.
`bv` names it at `detail` verbosity and moves on.

A pin is what one file says about one id.
Ten projects sharing one `Directory.Build.props` reference have one pin between them.
One file stating one id at two versions, one per target framework, has two.

### Buildvana's own packages

`bv`, `Buildvana.Sdk`, and `Buildvana.Runtime` are released in lockstep, and must stay in lockstep, so `bv dependencies` never sees them, in any scope.
[`bv self-update`](self-update.md) is the command that moves them, all at once.

### Additional package groups

A repository may pin package versions in files of its own, under an item name of its own.
Buildvana itself is the example.
The packages Buildvana SDK injects into projects are pinned as `BV_PackageVersion` items in `src/Buildvana.Sdk/Sdk/PackageVersions.props`, a file no project of this repository imports.
Such groups are declared in `buildvana.jsonc`:

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

`bv` evaluates each file on its own, so conditions, properties, and metadata mean there what they mean everywhere.
An item an import brings in from outside the glob of the group belongs to whatever file declares it, and is not the group's.
A file two groups match belongs to the first that names it.

### File-based apps

A file-based app states its dependencies in the `#:` directives of its leading block, and those are pins like any other.
`#:package Serilog@4.0.0` belongs to the `packages` scope, and `#:sdk Microsoft.Build.Traversal@4.1.0` to the `sdks` scope.
The `.cs` file that holds them is what an update edits.

A versionless directive names no version, so it is no pin.
It is a reference to a pin declared elsewhere.

The `fileBasedApps` setting says which `.cs` files are apps, and the hooks directory is always included.

---

## `bv dependencies show`

```text
bv dependencies show [OPTIONS]
bv dependencies [OPTIONS]
```

`show` lists the pins of every selected scope, with the policy governing each, and everything else that can be said without a network:

- the pins nothing can move, each with the reason;
- the pins that state a prerelease under a policy taking only stable versions, which no update moves and no update undoes;
- a `global.json` whose `sdk.allowPrerelease` disagrees with the `netsdk` policy, which is derived state an apply run writes.

It ends with the [transitive overrides](#transitive-overrides) in effect, each under the generated file that states it.
Those are the state the last apply run left behind.
Whether they are still needed is a question a restore answers, and `show` runs none.

Pins are grouped by the file that declares them, and the pins of an additional group appear under its caption.
A selected scope with no pins says that it has none.

A pin takes one line, `Serilog 3.0.0 (minor)`, and a note about it takes another, indented under it.
The report has no columns.
At the eighty columns of a CI log, a column layout divides the width among the columns, and breaks ids and versions across lines.
Neither is readable in halves.

The command works offline.
The MSBuild evaluation it runs for the `packages` scope is local work, with the same preconditions as building at all.
It always exits 0 when it completes.
Everything it reports is a finding, and what to do about it is your call.

---

## `bv dependencies update`

```text
bv dependencies update [ID...] [OPTIONS]
```

`update` moves every pin of every selected scope as far as its policy allows, and no further.
Its report gives each pin the line the `show` report gives it, with an arrow after it: `Serilog 3.0.0 (minor) -> 3.1.0 (latest: 3.1.0, 4.0.0-preview.2)`.
The arrow points at the version the pin moves to, or at the words that say why it moves nowhere.
Those words are `up to date`, `disabled`, `not managed`, `not selected`, or `held`.
The versions in parentheses are the latest stable version the sources have, and the latest prerelease above it, when there is one.
They are what a deliberate pin edit starts from.
A pin already at its target is counted and left out, and `--check --all` lists those as well.

`--check` reports what would change and changes nothing, and exits 1 when anything would.
That is the staleness gate for CI.

Every pin is resolved before anything is written, so a run either has a target for each pin it manages or changes nothing at all.

### Where versions come from

Package versions come from the package sources of the repository, read through the client libraries of NuGet.
`bv` reads the whole configuration chain from the home directory upwards, every enabled source type, and package source mapping.
What `bv` sees is therefore what a restore sees.
Authenticated sources are reached with the credential providers of the machine, without interaction.
`bv` never stops to ask, because a command that did would hang a CI run instead of failing it.

.NET SDK versions come from the official .NET release index, which also says whether a release is long-term support.

Two things stop a run, and both leave the repository untouched:

- a source that cannot answer.
  A resolution against the sources that happened to reply could only be wrong in silence.
  An "up to date" report is a claim, not a guess.
- a pin naming a package, or a version, that no source has.
  That is the repository's own error, a mistyped id or a source missing from `nuget.config`.
  One run reports every one of them, each naming the file that declares the pin.
  [The BV12xx diagnostics](../tool-diagnostics.md#dependency-management-1200-1299) list them.

A version some source knows and has delisted is not that error.
Delisting often means the version is vulnerable, so moving away from it is the remedy.
The pin is reported, and the update proceeds.

### What a run writes, and in what order

A run writes the `packages` scope, then `tools`, then `sdks`, and `global.json` last of all.

Package pins and project SDK pins are spliced in the file that declares them.
Only the version text changes, so formatting, comments, attribute order, and encoding survive byte for byte.
A pin declared twice at the same version, once per target framework, moves in both places, because MSBuild evaluated the two as one pin.

A tool is handed to `dotnet tool update <id> --local --version <target>`, one tool at a time.
The CLI then keeps the manifest and the installed tools in agreement.
`--all` is unusable here.
For a tool pinned to a prerelease line, it insists on the latest stable version, which is a downgrade, and then refuses to do it.

`global.json` goes last because a `global.json` naming an SDK that is not installed breaks every `dotnet` invocation after it.
`rollForward` never rolls down to an older patch.
`sdk.allowPrerelease` is written along with it, to say what the `netsdk` policy says, and added when the file states none.

### Naming the pins a run is about

Arguments name the pins a run is about, as package ids or as globs.
`bv deps update Microsoft.CodeAnalysis.*` is about those pins alone.
A filter that matches nothing is not an error, and the report says that there was nothing to do.

The .NET SDK has no package id, so a run that names pins leaves the baseline alone.
Passing `--netsdk` next to such an argument states the contradiction outright, and is a usage error.

### Moving a pin past its policy

`--to <VERSION>` states the version the named pins must reach.
It is an assisted manual edit, so it overrules the policy.
It moves a pin whose policy is `disable`, it crosses a prerelease line, and it is the one move that may lower a pin.
It does not go with `--check`, which writes nothing by definition.

Two forms exist:

- with one argument naming a package id, every pin of that id in the selected scopes takes the version.
  It is an error when no source has that version, and when the id has no pin `bv` manages.
  A Buildvana family package is always the second case, because [`bv self-update`](self-update.md) moves its pins as one.
- with no argument and `netsdk` as the only selected scope, `global.json` takes the version.
  Any other selected scope alongside is a usage error.

`--latest` moves the named pins to the latest version the sources have, whatever the kind of their policy.
The prerelease flag of the policy still holds.
Under `patch`, a pin lands on the latest stable version.
Under `patch-`, it lands on the latest version, prerelease included.
A pin whose policy is `disable` moves like any other, and so does the .NET SDK under `lts`.
`--latest` takes ids and patterns, several at once, as a plain run does.
With no argument, it applies to every pin of the selected scopes, the .NET SDK included.
Pass `--no-netsdk` to move the packages and the tools and leave the .NET SDK where its policy holds it.
`--latest` and `--to` each say where the named pins go, so they do not go together.
Like `--to`, `--latest` does not go with `--check`.

Some packages depend on each other at one version.
`Microsoft.CodeAnalysis.CSharp` depends on `Microsoft.CodeAnalysis.Common` at exactly its own version, so a restore with the two pins at different versions fails with NU1605.
Move such packages in one run, with `--latest` and a pattern:

```shell
bv deps update Microsoft.CodeAnalysis.* --latest
```

`--to` takes one id, so moving the same packages to a version that is not the latest takes one run per package.
Every run but the last writes its pin, and then fails with exit code 3 when the [transitive overrides](#transitive-overrides) restore the solution.
The `deps/post-update` hook does not run, and no report is written.
The last run finds the pins in agreement, and the restore succeeds.

### The `deps/post-update` hook

A repository that derives something from what it pins updates what it derives in the `deps/post-update` hook.
A property naming a compiler version, or a floor implied by a package, are examples.
The hook runs at the end of every `update` that ran to completion, check runs included.
In a check run, exit code 1 from the hook says that it would change something.
The command folds that code into its own exit code.
[Hooks](../hooks.md#the-depspost-update-hook) describes the hook.

---

## `bv dependencies prune`

```text
bv dependencies prune [OPTIONS]
```

`prune` removes the central package pins nothing references any more.
Such pins accumulate on their own: `dotnet package remove` deletes the reference and leaves the pin behind, and nothing else removes it.

A pin is _orphaned_ when no project of the solution references its package directly, and no versionless `#:package` directive names it.
NuGet gives the answer.
`prune` restores the solution, and reads what the dependency graph of each project states about its own direct references.
A textual scan of the MSBuild files could not answer as much.
A reference written through a property or an item transform is one only an evaluation sees.
The restore leaves the [transitive override files](#transitive-overrides) out of the evaluation, so a promotion `bv` wrote never makes a pin look alive.

That restore is why the diagnosis lives here rather than in the offline `show`.
Removing an entry and moving a version forward are different actions with different consequences, so they are different commands.

Only central pins can be orphaned.
A `PackageReference` is itself the reference, and so is a `#:package` directive that carries a version, so neither can outlive anything.
The `netsdk`, `sdks`, and `tools` scopes are ignored in silence.
A run whose scope selection leaves out the `packages` scope does nothing and succeeds.

What makes a pin central is its item type, not the file that declares it.
A `PackageVersion` item an [additional package group](#additional-package-groups) declares is a central pin, and is treated like one.
A group whose items carry a name of its own, `BV_PackageVersion` say, declares no central pin, and `prune` passes over it.

Transitive pinning changes the question a project answers.
A project with `CentralPackageTransitivePinningEnabled` set raises the version of a package it never references, from the central pin alone.
A direct reference is then no longer what keeps a pin alive there.
`bv` reads the whole resolved graph of such a project instead: a pin whose package the project resolves, at any depth, is in use.
A pin whose package the graph never resolves raises nothing, and is an orphan there as anywhere else.

The answer covers what the restore evaluated: the projects of the solution, and the target frameworks each of them states.
A repository that conditions its own `TargetFrameworks`, or that keeps a project out of the solution, hides references from that evaluation.
A pin used only there looks orphaned, and `prune` removes it.

Policy plays no part.
A policy says how far a pin may move, and says nothing about whether the repository still needs it.
A pin whose policy is `disable` is removed like any other.

`prune --check` reports the orphaned pins and removes none, and exits 1 when there is at least one.
No argument names the pins a run is about.
An orphan is a pin nothing references.
A filter that hid one would leave the repository stating a pin the same run had just called dead.

A removal can change what the [transitive overrides](#transitive-overrides) must say, because a promotion may rest on the pin that has just gone.
A promoted reference whose central pin is missing is one the next restore rejects.
An apply run therefore regenerates the override files before it ends, whether or not it removed anything, as `update` does.
The diagnosis restores with those files left out of the evaluation.
No build may be left with the graph that produces.
A check run regenerates nothing, so it restores once more with the files in place.
The `deps/post-update` hook then runs, whether or not anything was removed.
It runs at the end of every `prune` that managed the `packages` scope and ran to completion.
Every pin reaches it as skipped, because `prune` resolved none, and a pin the run removed does not reach it at all.

---

## Transitive overrides

A repository receives packages it never references: one package it does reference brings another, which brings a third.
When a security advisory covers the version one of those ends up at, `bv dependencies update` lifts it.
It writes a generated file forcing a higher version, and Buildvana SDK imports the file.

Two kinds of file carry the overrides, and both belong to `bv`:

- `Directory.TransitiveOverrides.props`, in the home directory, states the versions for the projects that manage their package versions centrally;
- `<ProjectName>.TransitiveOverrides.props`, beside a project, promotes to a reference of that project each package the project must lift.

Do not edit either file.
Every apply run that manages the `packages` scope rewrites both from scratch, which is also why a stale override needs no cleaning up.
It is not written again.
A promotion carries `PrivateAssets="all"`, so a package a project produces declares the dependencies it would declare without the file.

Only vulnerability lifting is automatic.
An override written for it is a lower bound, and its source is machine-readable.
It disappears on its own once the package that brought the vulnerable version raises its own floor.
Every other reason to force a transitive version, holding one back or unifying a version across the solution, has none of those properties.
Write those as an ordinary reference with a pin, and let the policy of the pin say what may move it.

### What a run does

The overrides are computed at the end of an apply run, after the pins are written and before the hook.
A check run leaves them alone, because predicting what a restore would find is a restore.

1. Restore, with the override files left out of the evaluation.
   That is the answer of NuGet on the graph as the repository states it, which is what the overrides are computed from.
2. Read the dependency graph of each project, and take the audit findings of the restore from it.
   `bv` does not re-implement the audit, so every NuGet audit setting is honored under the rules of NuGet: `NuGetAudit`, `NuGetAuditMode`, `NuGetAuditLevel`, `NuGetAuditSuppress`, `NoWarn`, and `<auditSources>`.
   What a project does not report is not lifted.
3. For each vulnerable package, take the lowest version its sources list that lies outside every advisory known for it.
   The version must lie within the update policy of the package, above the version the project resolves.
   A package the repository already pins safely gets no version of its own.
   The project is promoted at the pin, and no new version appears anywhere.
4. Write the files, restore again with them in place, and repeat from step 2 until the graph stops changing.
   Promotion can itself change the graph, which is why the graph is read again.
   Each pass writes everything selected so far in the run, because the graph of a later pass no longer reports what an earlier one lifted.

The whole repository is covered, whatever the invocation named.
A run aimed at one package can therefore end with a warning about another: the files describe the graph, not the command line.

### What no override may lift

Two things end a run with a warning that changes no exit code.
The vulnerability stays, NuGetAudit keeps reporting it at every build, and what to do about it is your call.

The first is a package no version can lift.
None the sources list falls outside every advisory, or the only one that does lies beyond what the policy of the package allows.
Widening the policy for that package, through a `dependencies.policies` entry, is what lets the next run lift it.

The second is a package a decision of the repository governs.
`bv` never introduces a version for a package the repository pins or references itself.
A vulnerable direct reference, a vulnerable central pin, and a central pin below the version a project resolves are all left alone.
Move the pin, or suppress the advisory through `NuGetAuditSuppress`.

---

## What Buildvana SDK contributes

Buildvana SDK contributes two things, both driven by `bv`, and neither changes an ordinary build:

- the target that dumps the evaluated package items of a project.
  `bv dependencies` runs it over the solution to see the `packages` scope as a build sees it.
  `bv` takes the pins from the evaluation rather than from the files.
  Conditions, imports, and layered central package management then mean the same thing to `bv` as to a build.
  The dump also carries the two values the [transitive overrides](#transitive-overrides) need from an evaluation.
  One is where a restore writes the dependency graph of that project.
  The other is the severity the audit of the project reports from.
- the import of the [transitive override files](#transitive-overrides), where they exist.
  A repository whose graph needs none has none, and the import finds nothing.

Both are driven by [internal-use properties](../internal-use-properties.md#dependency-management) that `bv` passes on the command line.

---

## Options

| Subcommand                | Option           | Meaning                                                                              |
| ------------------------- | ---------------- | ------------------------------------------------------------------------------------ |
| `show`, `update`, `prune` | `--netsdk`       | Manage the .NET SDK version pinned in `global.json`.                                 |
| `show`, `update`, `prune` | `--sdks`         | Manage the MSBuild project SDKs.                                                     |
| `show`, `update`, `prune` | `--tools`        | Manage the .NET local tools.                                                         |
| `show`, `update`, `prune` | `--packages`     | Manage the NuGet package pins.                                                       |
| `show`, `update`, `prune` | `--no-netsdk`    | Leave the .NET SDK version alone.                                                    |
| `show`, `update`, `prune` | `--no-sdks`      | Leave the MSBuild project SDKs alone.                                                |
| `show`, `update`, `prune` | `--no-tools`     | Leave the .NET local tools alone.                                                    |
| `show`, `update`, `prune` | `--no-packages`  | Leave the NuGet package pins alone.                                                  |
| `update`, `prune`         | `--check`        | Report what would change, change nothing, and exit 1 when anything would.            |
| `update`                  | `--all`          | List every pin in the report, not only the ones with news. Only with `--check`.      |
| `update`                  | `--to <VERSION>` | Set the named pins to this version, whatever their policy says, downgrades included. |
| `update`                  | `--latest`       | Move the named pins to the latest version, past their policy.                        |

`update` also takes arguments, the ids or globs naming the pins the run is about, as [Naming the pins a run is about](#naming-the-pins-a-run-is-about) says.

---

## Exit codes

The dependency commands return the [exit codes every `bv` command returns](../tool-diagnostics.md#exit-codes), with no meaning of their own added.

Code 1 is the result of `update --check`: a pin has fallen behind its policy, or the hook says it would change something.
It is the result of `prune --check` as well, where it says that the repository states a pin nothing references.
Nothing failed, and nothing was written.
It is also the code of every error above that stops a run before it writes.
Those are a pin the sources do not know, a source that cannot be reached, and a version `--to` names and no source has.
One failure of its own carries it too: transitive overrides that never stop changing.
No program failed there, and the procedure that gave up is the one of `bv`.

Code 2 is a refusal of the command line itself.
The refusals are scope options of both families at once, `--all` without `--check`, `--to` with `--check`, `--latest` with `--check`, and `--latest` with `--to`.
Three more are `--netsdk` next to an argument naming pins, `--to` naming the .NET SDK beside another scope, and a version that does not parse.

Code 3 is the one a reader of a report should know about.
It says that a program `bv` ran failed, or answered with something `bv` cannot read.
The pins of the `packages` scope come from an MSBuild evaluation.
A report missing that scope would otherwise read as a repository with no packages.
A failed `dotnet tool update` and a failed hook are two more.
The [transitive overrides](#transitive-overrides) add two of their own.
One is a restore that failed for a reason other than its own audit findings.
The other is a restore that could not read a package source in full.
The restore [`prune`](#bv-dependencies-prune) runs carries the first of the two, and not the second, because what a project references does not depend on vulnerability data.
Overrides regenerated from a fraction of the advisories would delete one that is still needed.
An incomplete answer therefore stops the run and leaves every file as it stands.
