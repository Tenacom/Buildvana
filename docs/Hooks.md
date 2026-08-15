# Hooks

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Overview](#overview)
- [The `release/post-release` hook](#the-releasepost-release-hook)
- [Writing a hook](#writing-a-hook)
- [The hook args](#the-hook-args)
- [The repository configuration](#the-repository-configuration)
- [Dependencies](#dependencies)
- [The build environment](#the-build-environment)
- [Cleaning hook build caches](#cleaning-hook-build-caches)
- [Contract evolution](#contract-evolution)

## Overview

A hook is real code, owned by the repository, that `bv` runs when a well-known event occurs: a [file-based app](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/program-structure/file-based-programs) (a standalone C# file) acting as an event handler.

Hooks live at well-known paths of the form `.buildvana/hooks/<context>/<event>.cs`. `<event>` names the event: the moment of execution that triggers the hook. `<context>` names the context the event belongs to — currently always the invoking command, though nothing ties a context to being a command. If Buildvana were an object and hooks were functions, `.buildvana/hooks/release/post-release.cs` would be the `Release_PostRelease` handler.

Exactly one event exists today: `release/post-release`. A hook is optional; when the file is absent, `bv` skips it with an info message.

Whatever the context and event, a hook is guaranteed to run with the home directory as its working directory: relative paths in a hook resolve against the repository root, as the example below relies on.

## The `release/post-release` hook

During a release, `bv` rewrites the three well-known self-reference files (`global.json`, `.config/dotnet-tools.json`, `Directory.Packages.props`) when dogfooding is enabled. But a repository can embed the released version in arbitrary other files; this hook is the escape hatch for those.

`.buildvana/hooks/release/post-release.cs` runs at the moment the post-release commit is assembled: before the well-known self-reference rewrites (when dogfooding is enabled) and before anything is pushed. The hook runs whether or not dogfooding is enabled; the `dogfood` option gates only the built-in rewrites, and the args tell the hook which way it resolved.

The hook runs _before_ the built-in rewrites, rather than after, because it is a file-based app inside the repository tree: building it resolves the repository's own version pins, and the version being released is on no feed until the release completes. At hook time, therefore, `global.json`, `.config/dotnet-tools.json`, and `Directory.Packages.props` still carry the previously published versions; the version being released is in the args, not yet in the files.

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
if (!args.Dogfooding)
{
    return;
}

var text = File.ReadAllText("some-file.md");
text = Regex.Replace(text, "(MyOrg/MyRepo/)[^/]+(/docs/)", $"${{1}}{args.Release.SemVer}$2");
File.WriteAllText("some-file.md", text);
```

Because the version pin is applied by the SDK rather than gated on anything `bv` passes, a hook stays buildable and runnable by hand: after `bv` has run the hook once, `dotnet run` it from the home directory to replay it against the args of the last run (or against a hand-written args file).

The well-known paths themselves ship in the package too: `WellKnownPaths` exposes the hook and args directories plus per-hook path helpers, so repository tooling can compute these paths instead of hard-coding them.

## The hook args

`bv` serializes the args of the run to a per-hook file, `.buildvana-temp/hook-args/<context>/<event>.json` in the home directory — `.buildvana-temp/hook-args/release/post-release.json` for this hook — (re)writing the file before each hook run and leaving it in place afterwards; this is what makes hooks replayable by hand. Its content is logged at trace level, visible at `diagnostic` verbosity. `PostReleaseHookArgs.Load()` reads and deserializes it; the members are:

| Member                           | Type           | Content                                                                                                                                                                              |
| -------------------------------- | -------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `RuntimeInfo.Version`            | string         | The version of the `bv` running the hook, in semantic version form without build metadata.                                                                                           |
| `RuntimeInfo.DelegatingVersion`  | string or null | The version of the `bv` that [delegated](DirectoryStructure.md#configdotnet-toolsjson) the run to the version pinned in the tool manifest, or `null` when the run was not delegated. |
| `RuntimeInfo.HomeDirectory`      | string         | Absolute path of the home directory, without a trailing separator (also the hook's working directory).                                                                               |
| `RuntimeInfo.ArtifactsDirectory` | string         | Absolute path of the directory containing the build artifacts.                                                                                                                       |
| `RuntimeInfo.ScratchDirectory`   | string         | Absolute path of bv's scratch directory (`.buildvana-temp/`), where hooks can write temporary files without affecting working-tree change detection.                                 |
| `RuntimeInfo.ConfigFile`         | string or null | Absolute path of the configuration file this run read, or `null` when the repository has none. See [The repository configuration](#the-repository-configuration).                    |
| `RuntimeInfo.Configuration`      | object         | The resolved configuration of the run: every setting at its effective value. See [The repository configuration](#the-repository-configuration).                                      |
| `Release.Version`                | string         | The version being released, in simple `MAJOR.MINOR.PATCH` form, without any prerelease tag.                                                                                          |
| `Release.SemVer`                 | string         | The version being released, in full semantic version form. This is the form used by release tags and embedded in artifact names.                                                     |
| `Release.PreviousVersion`        | string or null | The previously released version (the latest release tag reachable from `HEAD`), or `null` when no previous release exists.                                                           |
| `Release.IsPrerelease`           | boolean        | Whether the version being released is a prerelease.                                                                                                                                  |
| `Release.IsPublicRelease`        | boolean        | Whether the release is a public release. Currently always `true`, since `bv release` requires a public release.                                                                      |
| `ProducedPackages`               | dictionary     | The packages produced by the release, mapping package ID to version.                                                                                                                 |
| `Dogfooding`                     | boolean        | Whether the built-in self-reference rewrites will run in this release — the resolved outcome, which the `--dogfood` flag may have overridden away from the configured value.         |

In the JSON file, member names are camelCase (`runtimeInfo.homeDirectory`, `release.semVer`, and so on); dictionary keys are serialized verbatim.

`.buildvana-temp/` is bv's scratch directory for machine-generated temporary files; add it to `.gitignore`. `bv` itself never mistakes its contents for hook-made changes — the directory is unconditionally excluded from working-tree change detection — but without the ignore entry, Git tooling will show the args files as untracked.

## The repository configuration

The args carry the facts of the run; for any standing repository setting, read the resolved configuration embedded in the args. `RuntimeInfo.Configuration` holds every setting at its effective value, with the configuration file, the command line, and the built-in defaults already composed (a repository with no configuration file resolves to all defaults), so a hook reads a setting by property access instead of spelling out its own fallback:

```csharp
var branches = hookArgs.RuntimeInfo.Configuration.Release.Branches;
```

The embedded configuration is a snapshot, taken when the args were written: a hook sees the effective settings of the very run its args belong to — replaying an args file by hand replays its settings too — not whatever the configuration file happens to say by the time the hook runs.

A hook that works on the configuration file _itself_ — rewriting a value in it, say — needs the path rather than the settings, and must act on the file `bv` actually read. That path is in the args, as `RuntimeInfo.ConfigFile` (`null` when the repository has no configuration file); do not hardcode a file name, and do not search for one:

```csharp
var configFile = hookArgs.RuntimeInfo.ConfigFile;
if (configFile is not null)
{
    File.WriteAllText(configFile, Rewrite(File.ReadAllText(configFile)));
}
```

## Dependencies

- `#:package Buildvana.Runtime` is special: its version comes from the SDK, not from central package management, so the pin can never lag or race the release.
- **Beyond that, prefer BCL-only hooks.** The BCL (including `System.Text.Json`) covers version-rewriting jobs.
- For third-party dependencies, prefer versionless `#:package` resolved through the repository's `Directory.Packages.props` (supported by file-based apps under central package management) — the version then lives where dependency updates already look.
- **Never reference self-produced packages via versionless `#:package`**: at hook time `Directory.Packages.props` still pins the previously published version, because the built-in rewrites happen after the hook — so the hook silently builds against the last release, and fails to compile, mid-release, against anything the release itself adds.
- `#:project` is the sanctioned way to use repo-local library code: no version pin, compiles against `HEAD`.
- Pinned `#:package Foo@x.y.z` is allowed but owned by the repository: pins drift on dependency updates, and a pin on a self-produced package lags its own release by one. If you break your own repository, you own both pieces.

## The build environment

Hooks require Buildvana SDK, which reaches them through the repository's `Directory.Build.{props,targets}` parent-inclusion chain (see [Directory structure](DirectoryStructure.md#directorybuildprops-and-directorybuildtargets)). A repository may add its own `.buildvana/Directory.Build.{props,targets}`, but they must follow the well-known parent-inclusion pattern; otherwise hooks break and the repository owns both pieces.

Hooks also inherit the rest of the repository's implicit build files (`nuget.config`, `global.json`, analyzer configuration): a hook compiles under the same rules as the rest of the repository, warnings-as-errors included.

## Cleaning hook build caches

Local file-based-app caching may not notice implicit-build-file changes; CI is always a cold build. `bv clean` clears the build cache of each `*.cs` file under `.buildvana/hooks/` (recursively), deleting its file-based-app artifacts directory. It also deletes the `.buildvana-temp/` scratch directory, last hook args file included.

## Contract evolution

The args file is written by the installed `bv` and read through the `Buildvana.Runtime` version pinned by the repository's Buildvana SDK — the version of the SDK in use, which `bv` refuses to run against unless it matches its own. The hook is compiled from source at every run, and its args file is rewritten immediately before it. Writer and reader are therefore the same version by construction, and the JSON never has to survive a version boundary.

What must stay stable is the _source_ surface a hook compiles against: members are never removed or repurposed, so that a hook written today still compiles after an update. Additions may be required members — every run then states every fact the args carry, and none can be left unset by mistake. (An args file left over from a run that predates such an addition no longer loads; re-run the command that raises the hook, and it is rewritten.)
