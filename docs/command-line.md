# Command line

This page says how to invoke `bv`, and what every command has in common.
The common part is the global options, the verbosity, the output streams, cancellation, the home directory, delegation, and the SDK version check.
The page ends with tips, with the remedies for the most common failures, and with the migration from the environment variables `bv` no longer reads.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Invocation](#invocation)
  - [Commands](#commands)
  - [Help](#help)
  - [Options and arguments](#options-and-arguments)
- [Global options](#global-options)
- [Verbosity](#verbosity)
- [Output streams](#output-streams)
- [Color](#color)
- [Console encoding](#console-encoding)
- [Cancellation](#cancellation)
- [Home directory](#home-directory)
- [Delegation](#delegation)
  - [When `bv` delegates](#when-bv-delegates)
  - [What the delegated run gets](#what-the-delegated-run-gets)
- [The SDK version check](#the-sdk-version-check)
- [Tips](#tips)
  - [Running `bv` in CI](#running-bv-in-ci)
  - [Running an exact version](#running-an-exact-version)
  - [Bisecting a regression of Buildvana SDK](#bisecting-a-regression-of-buildvana-sdk)
- [Troubleshooting](#troubleshooting)
  - [The SDK version check fails](#the-sdk-version-check-fails)
  - [The home directory is not found](#the-home-directory-is-not-found)
  - [Two configuration files](#two-configuration-files)
- [Migration from the removed environment variables](#migration-from-the-removed-environment-variables)

---

## Invocation

`bv` is a .NET tool, and runs in three ways:

- `dotnet bv <command>` runs the `bv` that the tool manifest, `.config/dotnet-tools.json`, pins, after a `dotnet tool restore`.
- `bv <command>` runs the global tool that `dotnet tool install -g bv` installed.
- `dnx bv@<version> <command>` runs one version, without installing it.

Whichever `bv` you invoke, the pinned `bv` is the one that runs when the tool manifest pins one.
[Delegation](#delegation) says how.

A command line holds a command, the arguments and options of the command, and the global options.

### Commands

| Command               | Aliases                             | SDK version check | Page                                                   |
| --------------------- | ----------------------------------- | ----------------- | ------------------------------------------------------ |
| `clean`               |                                     | no                | [Build pipeline](tool-commands/build-pipeline.md)      |
| `restore`             |                                     | yes               | [Build pipeline](tool-commands/build-pipeline.md)      |
| `build`               |                                     | yes               | [Build pipeline](tool-commands/build-pipeline.md)      |
| `test`                |                                     | yes               | [Build pipeline](tool-commands/build-pipeline.md)      |
| `pack`                |                                     | yes               | [Build pipeline](tool-commands/build-pipeline.md)      |
| `dependencies show`   | `dependencies`, `deps show`, `deps` | yes               | [Dependency management](tool-commands/dependencies.md) |
| `dependencies update` | `deps update`                       | yes               | [Dependency management](tool-commands/dependencies.md) |
| `dependencies prune`  | `deps prune`                        | yes               | [Dependency management](tool-commands/dependencies.md) |
| `release`             |                                     | yes               | [Release](tool-commands/release.md)                    |
| `self-update`         |                                     | no                | [Self-update](tool-commands/self-update.md)            |
| `version show`        | `version`                           | no                | [Version](tool-commands/version.md)                    |
| `version advance`     |                                     | no                | [Version](tool-commands/version.md)                    |

An alias runs the same command under another name, and help states it as an alias.
The "SDK version check" column says whether the command runs [the SDK version check](#the-sdk-version-check) first.

`dependencies` and `version` are command groups, and each has a default subcommand.
`bv dependencies` runs `bv dependencies show`, and `bv version` runs `bv version show`.

### Help

`bv --help`, or `bv` alone, prints the global options and the commands.
`bv <command> --help` prints the usage line, the arguments, and the options of a command, and lists its aliases.
For a command group, `bv <group> --help` lists the subcommands, and marks the default one.
`-h` is accepted wherever `--help` is.

### Options and arguments

An option name is matched without regard to case, so `--Check` and `--check` are one option.
An option that takes a value accepts it as the next token or after `=`, so `--to 2.1.0` and `--to=2.1.0` are equivalent.
A blank value counts as no value.
An argument may come before or after the options, so `bv deps update --check Serilog` names the same pin as `bv deps update Serilog --check`.

`bv` refuses a command line before running anything, with exit code 2, when it holds one of these:

- an option the command does not declare;
- an argument too many or too few;
- an option value that does not parse.

`bv restore`, `bv build`, `bv test`, and `bv pack` take no options of their own, and pass everything after `--` to `dotnet`, as [Forwarded arguments](tool-commands/build-pipeline.md#forwarded-arguments) says.
Every other command, `bv clean` included, refuses a `--` separator and everything after it, because it has nothing to forward to.

---

## Global options

A global option is accepted before or after the command name, and every command accepts all of them.

| Option                      | Meaning                                                                |
| --------------------------- | ---------------------------------------------------------------------- |
| `-v`, `--verbosity <LEVEL>` | The verbosity of the run, as [Verbosity](#verbosity) says.             |
| `--color`                   | Color the narration, whatever the console is, as [Color](#color) says. |
| `--no-color`                | Never color the narration.                                             |
| `--nologo`                  | Do not print the startup logo.                                         |
| `--skip-sdk-check`          | Skip [the SDK version check](#the-sdk-version-check).                  |
| `--skip-delegation`         | Run the invoked `bv`, as [Delegation](#delegation) says.               |
| `--version`                 | Print the version of `bv` and exit.                                    |
| `-h`, `--help`              | Print help and exit.                                                   |

Before running a command, `bv` prints the startup logo, `Buildvana CLI tool v<version>`, on standard error.
`bv --version` prints the informational version of `bv` on standard output, and exits without printing the logo and without running a command.
When the tool manifest pins `bv`, `bv --version` answers for the pinned `bv`.
Pass `--skip-delegation` to ask the invoked one.

---

## Verbosity

`--verbosity` takes one of five levels, in any case, and each level has a short form:

| Level        | Short form |
| ------------ | ---------- |
| `quiet`      | `q`        |
| `minimal`    | `m`        |
| `normal`     | `n`        |
| `detailed`   | `d`        |
| `diagnostic` | `diag`     |

The default is `minimal`, which is the default of `dotnet build`.
The build pipeline commands pass the verbosity to every `dotnet` command they run, so `bv build` and `dotnet build` produce comparable logs.

Every line of the narration of `bv` carries a level label, and the verbosity says which labels are shown:

| Label      | Shown from   | Content                                                                       |
| ---------- | ------------ | ----------------------------------------------------------------------------- |
| `error:`   | `quiet`      | Something went wrong.                                                         |
| `warning:` | `minimal`    | Something looks wrong and is not fatal.                                       |
| `notice:`  | `minimal`    | A record of a fact: what changed, what was decided, what was skipped and why. |
| `info:`    | `normal`     | What `bv` is doing at the moment.                                             |
| `detail:`  | `detailed`   | A detail for following along closely.                                         |
| `trace:`   | `diagnostic` | Fine-grained diagnostic output, such as the content of a hook args file.      |

An activity line, such as `[1] Build: starting...` or `[1] Build: done (12.3s)`, is shown from `normal`.
At the default verbosity, a command therefore prints what it did, and not what it is doing.
`bv release` still records every change it made and everything it published, at `notice:`.
The tasks of Buildvana SDK show each level from the same verbosity.
A message logged by a task and one printed by `bv` therefore appear together.

---

## Output streams

`bv` writes its narration to standard error, and keeps standard output for the result of a command.
The narration is the leveled lines, the activity lines, and the startup logo.
The result is the report of `bv version show`, the report of `bv dependencies`, or the summary of `bv self-update`.
A child `dotnet` process is streamed live, each of its streams to the same stream of `bv`.
The output of `dotnet` is the result of a build pipeline command.

A result stays pipeable at any verbosity:

```shell
bv version show | some-parser
```

The narration of `bv` and the output of `dotnet` can be told apart:

```shell
bv build 2>bv.log
```

A script that captures the diagnostics of `bv` reads standard error, or joins the streams with `2>&1`.

When a command fails, `bv` prints the error line at every verbosity.
A failure that carries diagnostics, such as an invalid `buildvana.jsonc`, prints each one after it, in the form `path(line,column): error CODE: message`.
A terminal such as VS Code renders that prefix as a link.
When a `dotnet` command fails, the error message names the command and its exit code.
It repeats the first and last twenty lines of the output too.

---

## Color

`bv` colors the label of an `error:` line red, and the label of a `warning:` line yellow.
The other labels and every message keep the colors of the terminal.

By default, `bv` colors the narration when standard error is a terminal that interprets ANSI escape sequences, and [`NO_COLOR`](environment-variables.md#no_color) is not set.
On Windows, `bv` asks the console to interpret the sequences, and turns color off when the console refuses.
Elsewhere, `bv` reads [`TERM`](environment-variables.md#term).
A redirected standard error turns color off, and a redirected standard output does not, because the narration goes to standard error.
`--color` and `--no-color` win over the detection and over `NO_COLOR`.
With both options on one command line, the detection decides.

---

## Console encoding

`bv` sets the output and input encodings of the console to UTF-8 for its run, as `dotnet` and MSBuild do, and restores them on exit.
[`DOTNET_CLI_CONSOLE_USE_DEFAULT_ENCODING`](environment-variables.md#dotnet_cli_console_use_default_encoding) opts out, under the rule of the .NET CLI.

---

## Cancellation

Ctrl-C cancels the running command.
`bv` stops launching further steps, terminates the running `dotnet` child process and its process tree, prints `error: Operation cancelled.`, and exits with code 130.
A cancelled build may leave partial output behind, and `bv clean` removes it.

---

## Home directory

`bv` works from the [home directory](directory-structure.md#home-directory), and you can invoke it from any directory under it.
`bv` walks up from the current directory, that directory included, and stops at the nearest directory that holds a home marker.
[Location of the home directory](directory-structure.md#location-of-the-home-directory) lists the markers.
The directory it stops at becomes the home directory and the current directory of the process.
From then on, every relative path resolves against the home directory, the forwarded arguments included, wherever `bv` was invoked from.
A delegated run behaves the same, because `bv` starts it from the home directory.

A command that needs no home directory leaves the current directory alone.
`bv --version` and `bv --help` are two.

When no directory at or above the current one holds a marker, `bv` fails with exit code 1:

```text
error: Home directory not defined (no buildvana.json[c], .git, or .git/HEAD found at or above 'C:\work\scratch').
```

---

## Delegation

When the tool manifest pins `bv`, the pinned `bv` runs, whichever `bv` you invoke.
The global `ng` of the Angular CLI hands over to the install of the project the same way.
A repository then runs one `bv` on every machine, and a newer global tool never changes the outcome of a build.

### When `bv` delegates

The invoked `bv` decides before anything else, the logo and `--version` included.
It reads the command line no further than the command name and the global options.
It reads the `bv` entry of the tool manifest, matched without regard to case, as the .NET CLI matches it.
Three cases follow:

- The manifest has no `bv` entry, or the repository has no manifest.
  The invoked `bv` runs in place, and says nothing.
- The entry pins a version other than the invoked one.
  The invoked `bv` delegates, and prints `Delegating to bv <version> from this repository's tool manifest.` on standard error.
- The entry pins the invoked version.
  A `bv` that runs from the NuGet package cache, as `dotnet bv` and `dnx` run it, runs in place.
  Any other `bv`, such as a global tool at that version, delegates, so that the install of the manifest is the one that runs.

An entry with no version, or with a version that does not parse, is reported with a warning and treated as no pin.
A manifest that cannot be read is reported the same way.

Three things keep the invoked `bv` in place:

- `--skip-delegation`.
- The `self-update` command, whose job is to pin the repository to the invoked `bv`.
- The [`BV_DELEGATED`](environment-variables.md#bv_delegated) environment variable, which `bv` sets on the delegated run, so that a delegated run never delegates again.

A `bv` invoked outside a repository finds no manifest, and runs in place.

### What the delegated run gets

The invoked `bv` first makes sure that the pinned version is installed.
It probes the tool resolver cache of the .NET SDK, as `dotnet tool run` does.
When the version is missing, it runs `dotnet tool restore`, and streams the output of the restore.
A failed restore is a warning, and the run is attempted anyway.
The restore may have failed on another tool of the manifest.
When `bv` itself is missing, the delegated run fails with the message of the .NET CLI.

The invoked `bv` then runs `dotnet tool run bv` with the whole original command line.
The delegated run starts from the home directory and inherits the standard streams.
The invoked `bv` returns the exit code of the delegated run as it stands.
It parses no option beyond the global ones, validates nothing, and does not read `buildvana.jsonc`.
The command line and the configuration may be valid for the pinned version alone, and the pinned version decides on them.
One exception exists.
A global option that takes a value, given with none, such as a trailing `-v`, is refused before delegation.
The message is the same in every version.

---

## The SDK version check

`bv`, Buildvana SDK, and `Buildvana.Runtime` are released together, and work as one matched group.
A `bv` running against another version of Buildvana SDK disagrees with it in silence, on the shape of the hook args for example.
A half-updated repository, or a newer global tool against an older pin, are the usual causes.

Before running, a command that uses Buildvana SDK checks that [`global.json`](directory-structure.md#globaljson) pins `Buildvana.Sdk` at the version of the running `bv`.
The [Commands](#commands) table says which commands check.
`bv` compares the versions by SemVer precedence, prerelease part included, and ignores build metadata.
A missing `global.json`, a missing `msbuild-sdks` section, and a missing `Buildvana.Sdk` entry count as a mismatch.
On a mismatch, the command fails with exit code 1 and a message naming both versions, as [The SDK version check fails](#the-sdk-version-check-fails) shows.
`--skip-sdk-check` skips the check, for a deliberate mismatch.

---

## Tips

### Running `bv` in CI

Pin `bv` in the tool manifest, and restore it before the first command:

```shell
dotnet tool restore
dotnet bv pack
```

A CI runner and a developer machine then run the same `bv`.
A change of version is a change to the manifest, reviewed like any other.

### Running an exact version

`dnx` runs a tool at a version without installing it:

```shell
dnx bv@2.1.0 version show
```

When the tool manifest pins another version, the run delegates to it.
Add `--skip-delegation` to run the version you named.

### Bisecting a regression of Buildvana SDK

To build a repository against another version of Buildvana SDK, edit the `Buildvana.Sdk` pin in `global.json`, and pass `--skip-sdk-check` to every `bv` command:

```shell
bv build --skip-sdk-check
```

To move the whole repository to a version, `bv`, Buildvana SDK, and `Buildvana.Runtime` at once, run [`bv self-update`](tool-commands/self-update.md) from that version:

```shell
dnx bv@2.1.0 self-update
```

---

## Troubleshooting

### The SDK version check fails

```text
error: SDK version check failed: global.json pins Buildvana.Sdk 2.1.500-preview, but this bv is version 2.1.538-preview. Run 'bv self-update' to update this repository's pins to a single version, or pass --skip-sdk-check to skip this check.
```

The message names the pinned version and the running one.
Three remedies exist:

- When the repository is half updated, run `bv self-update` from the version it should be at, which pins every file to that version.
- When the invoked `bv` is the wrong one, run the right version with `dotnet bv <command>` or `dnx bv@<version> <command>`.
- When the mismatch is deliberate, pass `--skip-sdk-check`.

### The home directory is not found

```text
error: Home directory not defined (no buildvana.json[c], .git, or .git/HEAD found at or above 'C:\work\scratch').
```

Run `bv` from a directory under the home directory.
When the directory is right and holds no marker, create a `buildvana.jsonc` file holding `{}` in it.
[Location of the home directory](directory-structure.md#location-of-the-home-directory) lists the other markers.

### Two configuration files

```text
error: Multiple Buildvana configuration files found: C:\work\repo\buildvana.json, C:\work\repo\buildvana.jsonc. Keep only one.
```

`bv` reads `buildvana.jsonc` before running a command, so every command fails, `clean` included.
Merge the two files into one, and delete the other.
Buildvana SDK reports the same state as [BVSDK1005](sdk-diagnostics.md#buildvana-sdk-core-1000-1049).

---

## Migration from the removed environment variables

`bv` no longer reads an environment variable as the default value of an option.

| Variable                 | Option it defaulted      | Now                                                            |
| ------------------------ | ------------------------ | -------------------------------------------------------------- |
| `CONFIGURATION`          | `--configuration`        | Pass `-c`, or set `dotnet.configuration` in `buildvana.jsonc`. |
| `VERSION_SPEC_CHANGE`    | `--versionSpecChange`    | Pass `--bump` to `bv release`.                                 |
| `CHECK_PUBLIC_API_FILES` | `--checkPublicApiFiles`  | Pass `--check-public-api`, or set `release.checkPublicApi`.    |
| `UPDATE_SELF_REFERENCES` | `--updateSelfReferences` | Pass `--dogfood`, or set `release.dogfood`.                    |

[Release](tool-commands/release.md#options) describes the options of `bv release`, and [The Buildvana configuration file](configuration-file.md) describes the settings.

The fixed environment variables that carried the secrets and the endpoints of `bv release`, `GITHUB_TOKEN` among them, are gone too.
`buildvana.jsonc` names the variable that carries each one, as [Secret-carrying variables named by the configuration file](environment-variables.md#secret-carrying-variables-named-by-the-configuration-file) says.
