# Hooks

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Overview](#overview)
- [The `release/post-release` hook](#the-releasepost-release-hook)
- [Writing a hook](#writing-a-hook)
- [The hook args](#the-hook-args)
- [Loading the repository configuration](#loading-the-repository-configuration)
- [Dependencies](#dependencies)
- [The build environment](#the-build-environment)
- [Cleaning hook build caches](#cleaning-hook-build-caches)
- [Contract evolution](#contract-evolution)

## Overview

A hook is real code, owned by the repository, that `bv` runs when a well-known event occurs: a [file-based app](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/program-structure/file-based-programs) (a standalone C# file) acting as an event handler.

Hooks live at well-known paths of the form `.buildvana/hooks/<context>/<event>.cs`. `<event>` names the event: the moment of execution that triggers the hook. `<context>` names the context the event belongs to — currently always the invoking command, though nothing ties a context to being a command. If Buildvana were an object and hooks were functions, `.buildvana/hooks/release/post-release.cs` would be the `Release_PostRelease` handler.

Exactly one event exists today: `release/post-release`. A hook is optional; when the file is absent, `bv` skips it with an info message.

## The `release/post-release` hook

During a release, `bv` rewrites the three well-known self-reference files (`global.json`, `.config/dotnet-tools.json`, `Directory.Packages.props`) when dogfooding is enabled. But a repository can embed the released version in arbitrary other files; this hook is the escape hatch for those.

`.buildvana/hooks/release/post-release.cs` runs at the moment the post-release commit is assembled: after the well-known self-reference rewrites (when dogfooding is enabled) and before anything is pushed. The hook runs whether or not dogfooding is enabled; the `dogfood` option gates only the built-in rewrites.

The hook runs from the home directory and reports nothing back. `bv` snapshots the working tree before and after the hook; the files the hook changed join the post-release commit alongside the well-known rewrites (or constitute it entirely, when dogfooding is off or rewrote nothing).

**"post-release" names the post-release _commit_**, not the release itself: when the hook runs, nothing has been pushed or published yet, and a non-zero exit code aborts the entire release. Announcements and other externally-visible actions don't belong here.

## Writing a hook

A hook is an ordinary file-based app: top-level statements, run via `dotnet run`. The types a hook needs — the typed hook args and the typed repository configuration — ship in the `Buildvana.Runtime` package. Reference it with an unversioned `#:package` directive; Buildvana SDK pins the package to its own version for every file-based app built in the repository, so `bv`, the SDK, and hooks always agree on the shape of the data:

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

var args = PostReleaseHookArgs.Load();
if (!args.Dogfooded)
{
    return;
}

var text = File.ReadAllText("some-file.md");
text = Regex.Replace(text, "(MyOrg/MyRepo/)[^/]+(/docs/)", $"${{1}}{args.Release.SemVer}$2");
File.WriteAllText("some-file.md", text);
```

Because the version pin is applied by the SDK rather than gated on anything `bv` passes, a hook stays buildable and runnable by hand: after `bv` has run the hook once, `dotnet run` it from the home directory to replay it against the args of the last run (or against a hand-written args file).

## The hook args

`bv` serializes the args of the run to a per-hook file, `.buildvana-temp/hook-args/<context>/<event>.json` in the home directory — `.buildvana-temp/hook-args/release/post-release.json` for this hook — (re)writing the file before each hook run and leaving it in place afterwards; this is what makes hooks replayable by hand. Its content is logged at `Detail` verbosity. `PostReleaseHookArgs.Load()` reads and deserializes it; the members are:

| Member                           | Type           | Content                                                                                                                                                                              |
| -------------------------------- | -------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `RuntimeInfo.Version`            | string         | The version of the `bv` running the hook, in semantic version form without build metadata.                                                                                           |
| `RuntimeInfo.DelegatingVersion`  | string or null | The version of the `bv` that [delegated](DirectoryStructure.md#configdotnet-toolsjson) the run to the version pinned in the tool manifest, or `null` when the run was not delegated. |
| `RuntimeInfo.HomeDirectory`      | string         | Absolute path of the home directory (also the hook's working directory).                                                                                                             |
| `RuntimeInfo.ArtifactsDirectory` | string         | Absolute path of the directory containing the build artifacts.                                                                                                                       |
| `RuntimeInfo.ScratchDirectory`   | string         | Absolute path of bv's scratch directory (`.buildvana-temp/`), where hooks can write temporary files without affecting working-tree change detection.                                 |
| `Release.Version`                | string         | The version being released, in simple `MAJOR.MINOR.PATCH` form, without any prerelease tag.                                                                                          |
| `Release.SemVer`                 | string         | The version being released, in full semantic version form. This is the form used by release tags and embedded in artifact names.                                                     |
| `Release.PreviousVersion`        | string or null | The previously released version (the latest release tag reachable from `HEAD`), or `null` when no previous release exists.                                                           |
| `Release.IsPrerelease`           | boolean        | Whether the version being released is a prerelease.                                                                                                                                  |
| `Release.IsPublicRelease`        | boolean        | Whether the release is a public release. Currently always `true`, since `bv release` requires a public release.                                                                      |
| `ProducedPackages`               | dictionary     | The packages produced by the release, mapping package ID to version.                                                                                                                 |
| `Dogfooded`                      | boolean        | Whether the built-in self-reference rewrites ran in this release — the resolved outcome, which the `--dogfood` flag may have overridden away from the configured value.              |

In the JSON file, member names are camelCase (`runtimeInfo.homeDirectory`, `release.semVer`, and so on); dictionary keys are serialized verbatim.

`.buildvana-temp/` is bv's scratch directory for machine-generated temporary files; add it to `.gitignore`. `bv` itself never mistakes its contents for hook-made changes — the directory is unconditionally excluded from working-tree change detection — but without the ignore entry, Git tooling will show the args files as untracked.

## Loading the repository configuration

The args carry the facts of the run; for any standing repository setting, load the configuration file instead: `BuildvanaConfig.Load()` probes the four well-known candidates (`buildvana.json`, `buildvana.jsonc`, and the same names under `.buildvana/`), applies the usual exactly-one rule, tolerates comments and trailing commas, and returns the typed configuration (an empty instance when no configuration file exists):

```csharp
var config = BuildvanaConfig.Load();
var branches = config.Release?.Branches;
```

The loader is strict — an unknown member fails the load — but does not re-validate what `bv` has already validated with schema-based diagnostics before running any hook.

## Dependencies

- `#:package Buildvana.Runtime` is special: its version comes from the SDK, not from central package management, so the pin can never lag or race the release.
- **Beyond that, prefer BCL-only hooks.** The BCL (including `System.Text.Json`) covers version-rewriting jobs.
- For third-party dependencies, prefer versionless `#:package` resolved through the repository's `Directory.Packages.props` (supported by file-based apps under central package management) — the version then lives where dependency updates already look.
- **Never reference self-produced packages via versionless `#:package`**: at hook time `Directory.Packages.props` has already been rewritten to the version being released, which is on no feed until the release completes — restore fails mid-release, deterministically.
- `#:project` is the sanctioned way to use repo-local library code: no version pin, compiles against `HEAD`.
- Pinned `#:package Foo@x.y.z` is allowed but owned by the repository: pins drift on dependency updates, and a pin on a self-produced package lags its own release by one. If you break your own repository, you own both pieces.

## The build environment

Hooks require Buildvana SDK, which reaches them through the repository's `Directory.Build.{props,targets}` parent-inclusion chain (see [Directory structure](DirectoryStructure.md#directorybuildprops-and-directorybuildtargets)). A repository may add its own `.buildvana/Directory.Build.{props,targets}`, but they must follow the well-known parent-inclusion pattern; otherwise hooks break and the repository owns both pieces.

Hooks also inherit the rest of the repository's implicit build files (`nuget.config`, `global.json`, analyzer configuration): a hook compiles under the same rules as the rest of the repository, warnings-as-errors included.

## Cleaning hook build caches

Local file-based-app caching may not notice implicit-build-file changes; CI is always a cold build. `bv clean` clears the build cache of each `*.cs` file under `.buildvana/hooks/` (recursively), deleting its file-based-app artifacts directory. It also deletes the `.buildvana-temp/` scratch directory, last hook args file included.

## Contract evolution

The args file is written by the installed `bv` and read through the `Buildvana.Runtime` version pinned by the repository's Buildvana SDK; `bv` and the SDK are released in lockstep and designed as a matched pair. The contract is nevertheless additive-only: new members may be added, but existing ones are never removed or repurposed, and additions ship as optional members with default values — so an args file written before an update stays loadable after it.
