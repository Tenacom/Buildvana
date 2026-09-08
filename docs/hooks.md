# Hooks

A hook is a C# [file-based app](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/program-structure/file-based-programs) that the repository owns, and that `bv` runs when a well-known event occurs.
This page says which events exist, how to write a hook, and what `bv` passes to it.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Events and paths](#events-and-paths)
- [The `release/post-release` hook](#the-releasepost-release-hook)
- [The `deps/post-update` hook](#the-depspost-update-hook)
- [Writing a hook](#writing-a-hook)
- [The hook args](#the-hook-args)
- [The repository configuration](#the-repository-configuration)
- [Dependencies](#dependencies)
- [The build environment](#the-build-environment)
- [Cleaning hook build caches](#cleaning-hook-build-caches)
- [Contract evolution](#contract-evolution)

---

## Events and paths

A hook lives at `.buildvana/hooks/<context>/<event>.cs`.
`<event>` names the event: the moment at which `bv` runs the hook.
`<context>` names the context the event belongs to, which is always the invoking command.
Two events exist: `release/post-release` and `deps/post-update`.

A hook is optional.
When the file is absent, `bv` skips it and says so at `info` level.

`bv` runs every hook with the home directory as its working directory, so a relative path in a hook resolves against the home directory.

---

## The `release/post-release` hook

`bv release` rewrites three files to the version being released, when dogfooding is enabled: `global.json`, `.config/dotnet-tools.json`, and `Directory.Packages.props`.
A repository that embeds the version in other files rewrites them in this hook.

`bv release` runs `.buildvana/hooks/release/post-release.cs` while it assembles the post-release commit: after the artifacts are built, before the three rewrites, and before anything is pushed.
The hook runs whether or not dogfooding is enabled.
The `release.dogfood` setting gates the built-in rewrites alone, and the args tell the hook how the setting resolved.

The hook runs before the rewrites because it is a file-based app inside the repository tree.
Building it resolves the version pins of the repository, and the version being released is on no feed until the release completes.
At hook time, the three files still carry the versions published before, and the args carry the version being released.

The hook reports nothing back.
`bv` snapshots the working tree before and after the hook, and the files the hook changed join the post-release commit, together with the rewrites.
When dogfooding is off, or the rewrites changed nothing, the changes of the hook make up the whole commit.

"post-release" names the post-release commit, not the release.
When the hook runs, nothing has been pushed or published, and a non-zero exit code aborts the release.
Do not announce the release, or take any other outward-facing action, from this hook.

---

## The `deps/post-update` hook

A repository often derives something from what it pins.
One example is a property that names the compiler version of the pinned Roslyn.
Another is a table that must agree with the version of a tool.
`bv dependencies update` moves the pins, and this hook moves what the repository derives from them.

`.buildvana/hooks/deps/post-update.cs` runs at the end of every `bv dependencies update` that ran to completion, whether the run applied its updates or only checked them.
A run that stopped with an error never reaches the hook.
Derived state can drift on its own, so the hook runs even when no pin moved.

`bv dependencies prune` runs the hook too, because a pin it removed may be one the repository derived something from.
Only a run whose scope selection includes `packages` reaches it, because only a central package pin can be orphaned.
A `prune` run resolves no version, so every pin reaches the hook as `Skipped`.
A pin the run removed does not reach it at all.

The hook runs after the pins of the `packages`, `tools`, and `sdks` scopes are written, and before `global.json` is.
The `global.json` the hook reads still states the old .NET SDK version, and the args state the version the run is about to write.
`global.json` goes last because a `global.json` that names an SDK that is not installed breaks every `dotnet` invocation after it.
`rollForward` never rolls down to an older patch.

The args state what the run made of every pin of every selected scope, as [The hook args](#the-hook-args) lists.
A scope the invocation left out contributes nothing.
A pin an argument left out is stated as `Skipped`, so a hook that derives state from one pin sees that pin in every run.

The args also state the [transitive overrides](tool-commands/dependencies.md#transitive-overrides) in effect: the package, the version the generated file states, and the file stating it.
An apply run has just rewritten those files, and a check run reports what the last apply run wrote.

The hook has an exit-code convention of its own, because a check run has a verdict to give.

- In a check run, with `--check`, exit code 0 means that the hook found nothing to change.
  Exit code 1 means that it would change something.
  `bv` folds the 1 into its own verdict, as it folds a pin that has fallen behind.
  Any other exit code is a failure.
- In an apply run, exit code 0 means success, and any other exit code is a failure.

A hook that reads `Check` and writes nothing when it is `true` turns `bv dependencies update --check` into a complete staleness gate, for pins and derived state alike.

---

## Writing a hook

A hook is a file-based app: top-level statements, run through `dotnet run`.
The types a hook needs, the typed args and the typed configuration, ship in the `Buildvana.Runtime` package.
Reference it with an unversioned `#:package` directive.
The [`Hooks` module](sdk-modules/hooks.md) pins the package to the version of Buildvana SDK, for every file-based app built in the repository.
`bv`, Buildvana SDK, and the hooks then agree on the shape of the data.

```csharp
#:package Buildvana.Runtime
```

A complete hook that rewrites a version-pinned URL in a file:

```csharp
#:package Buildvana.Runtime

using System;
using System.IO;
using System.Text.RegularExpressions;
using Buildvana.Runtime;

var hookArgs = PostReleaseHookArgs.Load();
if (!hookArgs.Dogfooding)
{
    return;
}

var text = File.ReadAllText("some-file.md");
text = Regex.Replace(text, "(MyOrg/MyRepo/)[^/]+(/docs/)", $"${{1}}{hookArgs.Release.SemVer}$2");
File.WriteAllText("some-file.md", text);
```

Buildvana SDK applies the version pin, and nothing `bv` passes gates it, so a hook builds and runs by hand too.
After `bv` has run the hook once, run `dotnet run` on it from the home directory.
The hook then replays against the args of the last run, or against an args file written by hand.

`WellKnownPaths`, in the same package, exposes the hook and args directories, and a path helper per hook.
Repository tooling computes the paths through it instead of hard-coding them.

---

## The hook args

`bv` serializes the args of the run to a file per hook, `.buildvana-temp/hook-args/<context>/<event>.json` in the home directory.
For the release hook, that is `.buildvana-temp/hook-args/release/post-release.json`.
`bv` rewrites the file before each run of the hook and leaves it in place afterwards, which is what makes a hook replayable by hand.
`bv` logs the content at `trace` level, visible at `diagnostic` verbosity.
Each hook has an args type of its own, whose `Load()` method reads the file.
The `RuntimeInfo` section is the part every args type shares.

`PostReleaseHookArgs.Load()` reads the args of the `release/post-release` hook.
The members are:

| Member                           | Type           | Content                                                                                                                                                              |
| -------------------------------- | -------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `RuntimeInfo.Version`            | string         | The version of the `bv` running the hook, in semantic version form without build metadata.                                                                           |
| `RuntimeInfo.DelegatingVersion`  | string or null | The version of the `bv` that [delegated](command-line.md#delegation) the run to the pinned version, or `null` when the run was not delegated.                        |
| `RuntimeInfo.HomeDirectory`      | string         | The absolute path of the home directory, without a trailing separator. It is also the working directory of the hook.                                                 |
| `RuntimeInfo.ArtifactsDirectory` | string         | The absolute path of the directory holding the build artifacts.                                                                                                      |
| `RuntimeInfo.ScratchDirectory`   | string         | The absolute path of the scratch directory of `bv`, `.buildvana-temp/`, where a hook writes temporary files without affecting working-tree change detection.         |
| `RuntimeInfo.ConfigFile`         | string or null | The absolute path of the configuration file the run read, or `null` when the repository has none. See [The repository configuration](#the-repository-configuration). |
| `RuntimeInfo.Configuration`      | object         | The resolved configuration of the run: every setting at its effective value. See [The repository configuration](#the-repository-configuration).                      |
| `Release.Version`                | string         | The version being released, in `MAJOR.MINOR.PATCH` form, without a prerelease tag.                                                                                   |
| `Release.SemVer`                 | string         | The version being released, in full semantic version form. Release tags and artifact names use this form.                                                            |
| `Release.PreviousVersion`        | string or null | The version released before, as the latest release tag reachable from `HEAD`, or `null` when there is none.                                                          |
| `Release.IsPrerelease`           | boolean        | Whether the version being released is a prerelease.                                                                                                                  |
| `Release.IsPublicRelease`        | boolean        | Whether the release is a public release. Always `true`, because `bv release` requires a public release.                                                              |
| `ProducedPackages`               | dictionary     | The packages the release produced, from package id to version.                                                                                                       |
| `Dogfooding`                     | boolean        | Whether the built-in self-reference rewrites run in this release: the resolved value, which `--dogfood` may have set away from the configured one.                   |

`PostUpdateHookArgs.Load()` reads the args of the `deps/post-update` hook.
It carries the same `RuntimeInfo` section, plus:

| Member               | Type           | Content                                                                                                                                          |
| -------------------- | -------------- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| `Check`              | boolean        | Whether the run reports what it would do and changes nothing, as `--check` asks.                                                                 |
| `NetSdk`             | object or null | What the run made of the .NET SDK baseline, or `null` when the scope was not selected or the repository pins none.                               |
| `Sdks`               | array          | What the run made of the MSBuild project SDK pins.                                                                                               |
| `Tools`              | array          | What the run made of the .NET local tool pins.                                                                                                   |
| `Packages`           | array          | What the run made of the package pins that belong to no additional group.                                                                        |
| `AdditionalPackages` | array          | One entry per additional package group, in configuration order, each holding the `Caption` of the group and its `Results`.                       |
| `Overrides`          | array          | The transitive overrides in effect, each holding `PackageId`, `DeclaringFile`, and `Version`, which is `null` for a promotion without a version. |

Every one of those results, `NetSdk` included, states one pin:

| Member           | Type           | Content                                                                                                                     |
| ---------------- | -------------- | --------------------------------------------------------------------------------------------------------------------------- |
| `Id`             | string or null | The package id, or `null` for the .NET SDK baseline, which has none.                                                        |
| `DeclaringFile`  | string         | The path of the file that declares the pin, relative to the home directory, with forward slashes.                           |
| `CurrentVersion` | string         | The pin as it stood before the invocation.                                                                                  |
| `Target`         | string or null | The version the pin reached, or would reach in a check run, or `null` when there is none.                                   |
| `State`          | string         | One of `UpToDate`, `Updated`, `Disabled`, `Unmanaged`, `Skipped`, `Held`.                                                   |
| `LatestStable`   | string or null | The highest stable version the sources have, or `null` when nothing was resolved.                                           |
| `LatestPreview`  | string or null | The highest prerelease version the sources have above `LatestStable`, or `null` when there is none or nothing was resolved. |
| `Policy`         | string         | The policy governing the pin, in policy-string syntax.                                                                      |

In the JSON file, member names are camelCase: `runtimeInfo.homeDirectory`, `release.semVer`.
Dictionary keys and the names of enumeration values, such as the `State` of a result, are serialized as they are.

`.buildvana-temp/` is the scratch directory of `bv`, for machine-generated temporary files.
Add it to `.gitignore`.
`bv` never counts its content as a change a hook made, because the directory is excluded from working-tree change detection.
Without the ignore entry, Git tooling shows the args files as untracked.

---

## The repository configuration

The args carry the facts of the run.
For a standing setting of the repository, read the resolved configuration embedded in the args.
`RuntimeInfo.Configuration` holds every setting at its effective value, with the configuration file, the command line, and the built-in defaults composed.
A repository with no configuration file resolves to the defaults.
A hook reads a setting by property access, without a fallback of its own:

```csharp
var branches = hookArgs.RuntimeInfo.Configuration.Release.Branches;
```

The embedded configuration is a snapshot, taken when the args were written.
A hook sees the settings of the run its args belong to.
A replay by hand replays the settings too, whatever the configuration file says by then.

A hook that works on the configuration file itself, to rewrite a value in it, needs the path rather than the settings.
It must act on the file `bv` read.
`RuntimeInfo.ConfigFile` holds that path, or `null` when the repository has no configuration file.
Do not hard-code a file name, and do not search for one:

```csharp
var configFile = hookArgs.RuntimeInfo.ConfigFile;
if (configFile is not null)
{
    File.WriteAllText(configFile, Rewrite(File.ReadAllText(configFile)));
}
```

---

## Dependencies

- `#:package Buildvana.Runtime` is a special case.
  Its version comes from the [`Hooks` module](sdk-modules/hooks.md), not from central package management, so the pin can neither lag nor race the release.
- Beyond it, prefer a hook with BCL dependencies only.
  The BCL, `System.Text.Json` included, covers version-rewriting jobs.
- For a third-party package, prefer a versionless `#:package` directive, resolved through `Directory.Packages.props`.
  File-based apps support central package management, and the version then lives where dependency updates already look.
- Never reference a package the repository produces through a versionless `#:package` directive.
  At hook time, `Directory.Packages.props` still pins the version published before, because the built-in rewrites happen after the hook.
  The hook then builds against the last release, and fails to compile, mid-release, against anything the release adds.
- `#:project` is the way to use library code of the repository: no version pin, compiled against `HEAD`.
- A pinned `#:package Foo@x.y.z` is allowed, and the repository owns it.
  A pin drifts on dependency updates, and a pin on a package the repository produces lags its own release by one.

---

## The build environment

A hook requires Buildvana SDK, which reaches it through the `Directory.Build.props` and `Directory.Build.targets` of the home directory, as [Directory structure](directory-structure.md#directorybuildprops-and-directorybuildtargets) describes.
A repository may add a `Directory.Build.props` or a `Directory.Build.targets` under `.buildvana/`.
That file must then import its counterpart in the home directory, or the hooks break.

A hook also inherits the other implicit build files of the repository: `nuget.config`, `global.json`, and the analyzer configuration.
It compiles under the same rules as the rest of the repository, warnings as errors included.

---

## Cleaning hook build caches

The .NET SDK caches the build of a file-based app, and the cache may miss a change to an implicit build file.
`bv clean` clears the cache of every `.cs` file under `.buildvana/hooks/`, by deleting the artifacts directory of each.
It also deletes `.buildvana-temp/`, the last args files included.
A CI runner starts from an empty cache.

---

## Contract evolution

The installed `bv` writes the args file, and the hook reads it through the `Buildvana.Runtime` version that Buildvana SDK pins.
`bv` refuses to run against a Buildvana SDK version other than its own.
The hook is compiled from source at every run, and its args file is rewritten right before it.
Writer and reader are therefore the same version, and the JSON never has to survive a version boundary.

What stays stable is the source surface a hook compiles against.
A member is never removed or repurposed, so a hook written today still compiles after an update.
An addition may be a required member, so that every run states every fact the args carry.
An args file left over from a run before such an addition no longer loads.
Run the command that raises the hook again, and `bv` rewrites the file.
