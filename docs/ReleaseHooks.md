# Release hooks

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Overview](#overview)
- [The `release/post-release` hook](#the-releasepost-release-hook)
- [Writing a hook](#writing-a-hook)
- [The hook context](#the-hook-context)
- [Loading the repository configuration](#loading-the-repository-configuration)
- [Dependencies](#dependencies)
- [The build environment](#the-build-environment)
- [Cleaning hook build caches](#cleaning-hook-build-caches)
- [Contract evolution](#contract-evolution)

## Overview

During a release, `bv` rewrites the three well-known self-reference files (`global.json`, `.config/dotnet-tools.json`, `Directory.Packages.props`) when dogfooding is enabled. But a repository can embed the released version in arbitrary other files; hooks are the escape hatch for those. A hook is real code, owned by the repository: a [file-based app](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/program-structure/file-based-programs) (a standalone C# file) that `bv` runs at a named moment of a command.

Hooks live at well-known paths of the form `.buildvana/hooks/<command>/<moment>.cs`: the directory names the command, the file names the moment. Exactly one moment exists today: `release/post-release`. A hook is optional; when the file is absent, `bv` skips it with an info message.

## The `release/post-release` hook

`.buildvana/hooks/release/post-release.cs` runs at the moment the post-release commit is assembled: after the well-known self-reference rewrites (when dogfooding is enabled) and before anything is pushed. The hook runs whether or not dogfooding is enabled; the `dogfood` option gates only the built-in rewrites.

The hook runs from the home directory and reports nothing back. `bv` snapshots the working tree before and after the hook; the files the hook changed join the post-release commit alongside the well-known rewrites (or constitute it entirely, when dogfooding is off or rewrote nothing).

**"post-release" names the post-release _commit_**, not the release itself: when the hook runs, nothing has been pushed or published yet, and a non-zero exit code aborts the entire release. Announcements and other externally-visible actions don't belong here.

## Writing a hook

A hook is an ordinary file-based app: top-level statements, run via `dotnet run`. Buildvana SDK injects two support types into the compilation of every file-based app located under `.buildvana/hooks/`, so a hook starts with no scaffolding:

- `BvHookContext` — the context of the current run; load it with `BvHookContext.Load()`.
- `BvConfig` — a loader for the repository's Buildvana configuration file; load it with `BvConfig.Load()`.

A complete hook that rewrites a version-pinned URL in a file:

```csharp
using System;
using System.IO;
using System.Text.RegularExpressions;

var context = BvHookContext.Load();
if (!context.Dogfooded)
{
    return;
}

var text = File.ReadAllText("some-file.md");
text = Regex.Replace(text, "(MyOrg/MyRepo/)[^/]+(/docs/)", $"${{1}}{context.ReleaseSemVer}$2");
File.WriteAllText("some-file.md", text);
```

Because the injection is path-based rather than gated on anything `bv` passes, a hook stays buildable and runnable by hand — set `BV_HOOK_CONTEXT` to a hand-written context file and `dotnet run` the hook from the home directory to try it outside a release.

## The hook context

`bv` serializes the context of the run to a temporary JSON file and publishes its absolute path in the `BV_HOOK_CONTEXT` environment variable. The file is deleted when the hook completes; its content is logged at `Detail` verbosity. `BvHookContext.Load()` reads and parses it; the underlying JSON properties (camelCase) are:

| Property              | Type              | Content                                                                                                                     |
| --------------------- | ----------------- | --------------------------------------------------------------------------------------------------------------------------- |
| `homeDirectory`       | string            | Absolute path of the home directory (also the hook's working directory).                                                     |
| `releaseVersion`      | string            | The version being released, in simple `MAJOR.MINOR.PATCH` form, without any prerelease tag.                                  |
| `releaseSemVer`       | string            | The version being released, in full semantic version form. This is the form used by release tags and embedded in artifact names. |
| `previousVersion`     | string or null    | The previously released version (the latest release tag reachable from `HEAD`), or `null` when no previous release exists.   |
| `isPrerelease`        | boolean           | Whether the version being released is a prerelease.                                                                          |
| `isPublicRelease`     | boolean           | Whether the release is a public release. Currently always `true`, since `bv release` requires a public release.              |
| `artifactsDirectory`  | string            | Absolute path of the directory containing the build artifacts.                                                               |
| `producedPackages`    | object            | The packages produced by the release, mapping package ID to version.                                                         |
| `dogfooded`           | boolean           | Whether the built-in self-reference rewrites ran in this release — the resolved outcome, which the `--dogfood` flag may have overridden away from the configured value. |

## Loading the repository configuration

The context carries the facts of the run; for any standing repository setting, load the configuration file instead: `BvConfig.Load()` probes the four well-known candidates (`buildvana.json`, `buildvana.jsonc`, and the same names under `.buildvana/`), applies the usual exactly-one rule, tolerates comments and trailing commas, and returns the root `JsonElement` (an empty object when no configuration file exists):

```csharp
var config = BvConfig.Load();
var branches = config.GetProperty("release").GetProperty("branches");
```

## Dependencies

- **Prefer BCL-only hooks.** The BCL (including `System.Text.Json`) covers version-rewriting jobs.
- For third-party dependencies, prefer versionless `#:package` resolved through the repository's `Directory.Packages.props` (supported by file-based apps under central package management) — the version then lives where dependency updates already look.
- **Never reference self-produced packages via versionless `#:package`**: at hook time `Directory.Packages.props` has already been rewritten to the version being released, which is on no feed until the release completes — restore fails mid-release, deterministically.
- `#:project` is the sanctioned way to use repo-local library code: no version pin, compiles against `HEAD`.
- Pinned `#:package Foo@x.y.z` is allowed but owned by the repository: pins drift on dependency updates, and a pin on a self-produced package lags its own release by one. If you break your own repository, you own both pieces.

## The build environment

Hooks require Buildvana SDK, which reaches them through the repository's `Directory.Build.{props,targets}` parent-inclusion chain (see [Directory structure](DirectoryStructure.md#directorybuildprops-and-directorybuildtargets)). A repository may add its own `.buildvana/Directory.Build.{props,targets}`, but they must follow the well-known parent-inclusion pattern; otherwise hooks break and the repository owns both pieces.

Hooks also inherit the rest of the repository's implicit build files (`nuget.config`, `global.json`, analyzer configuration): a hook compiles under the same rules as the rest of the repository, warnings-as-errors included. The injected support sources are marked as generated code and are exempt from the consuming repository's analyzers.

## Cleaning hook build caches

Local file-based-app caching may not notice implicit-build-file changes; CI is always a cold build. `bv clean` runs `dotnet clean` on each `*.cs` file under `.buildvana/hooks/` (recursively), clearing its build cache.

## Contract evolution

The context file is written by the installed `bv` and read by loader code shipped with the repository's pinned Buildvana SDK; the two can transiently diverge. The contract is therefore additive-only: new properties may be added, but existing ones are never removed or repurposed. `BvHookContext.Load()` ignores unknown properties and gives missing ones their default values, so a version mismatch degrades gracefully instead of failing.
