# Diagnostics and exit codes of `bv`

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Overview](#overview)
- [Main program (1000-1099)](#main-program-1000-1099)
- [Configuration (1100-1199)](#configuration-1100-1199)
- [Dependency management (1200-1299)](#dependency-management-1200-1299)
- [Exit codes](#exit-codes)

## Overview

All diagnostics issued by the `bv` CLI tool have a `BV` prefix. All numbers start from 1000, so there are no leading zeros.

Each part of the program is assigned a contiguous range of 100 diagnostics, as listed below. The first range is reserved for the main program.

## Main program (1000-1099)

There are no associated diagnostics.

## Configuration (1100-1199)

| Code   | Severity | Message                                               | Description                                                                                                                                  |
| ------ | :------: | ----------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| BV1100 |  Error   | _(the JSON parser's reason)_                          | The configuration file could not be parsed as JSON. The message carries the parser's reason; the location points at the offending character. |
| BV1101 |  Error   | Expected _(type)_, but found _(type)_.                | A value has a type the schema does not allow at that location (for example, a number where a string is required, or an explicit `null`).     |
| BV1102 |  Error   | _(value)_ is not one of the allowed values: _(list)_. | A value is not among those the schema permits at that location (for example, an unknown enumeration value).                                  |
| BV1103 |  Error   | Unknown property '_(name)_'.                          | The configuration file contains a property the schema does not define, or a dictionary key outside the allowed set.                          |
| BV1104 |  Error   | Missing required property '_(name)_'.                 | A property the schema marks as required is absent.                                                                                           |
| BV1105 |  Error   | No value is allowed here.                             | A value appears at a location where the schema permits none.                                                                                 |
| BV1106 |  Error   | The value must not be empty.                          | A string value is shorter than the schema's minimum length. For a required string this means a stated member carries no actual value.        |
| BV1107 |  Error   | _(value)_ does not match the pattern '_(pattern)_'.   | A string value does not match the pattern the schema demands of it. For a required string this means the value is all whitespace.            |
| BV1108 |  Error   | Duplicate property '_(name)_'.                        | An object states the same property name twice. The location points at the repeated name; remove it, or merge the two into one property.      |

BV1106 and BV1107 also report a property _name_ that carries data, as the members of `dependencies.policies` and `dependencies.additionalPackages` do: a name is held to the same non-blank rule as any other required string, and the location points at the name rather than at the value it introduces.

## Dependency management (1200-1299)

| Code   | Severity | Message                                                       | Description                                                                                                                                      |
| ------ | :------: | ------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| BV1200 |  Error   | No configured package source knows _(id)_.                    | No source the repository configures has ever had the package. The id is mistyped, or the source that has it is missing from `nuget.config`.      |
| BV1201 |  Error   | No configured package source has _(id)_ _(version)_.          | The sources know the package, but not the version the repository pins, or the version `--to` states.                                             |
| BV1202 |  Error   | The .NET release index has no .NET SDK _(version)_.           | The version `global.json` pins, or the one `--to` states, is not a .NET SDK Microsoft published.                                                 |
| BV1203 |  Error   | No pin bv manages, in the selected scopes, has the id _(id)_. | `--to` states a version for an id that has no pin, or whose only pins are ones `bv` does not manage. A Buildvana family id is always this error. |

BV1203 has a second message, for an id of Buildvana's own package family: _(id)_ belongs to Buildvana's own package family, which moves in lockstep. Use bv self-update. Those pins move together with the SDK, so no scope of `bv dependencies` manages one.

Every one of these is reported by `bv dependencies update`, and every one of them stops the run before anything is written. One run reports all of them, each naming the file that declares the pin.

## Exit codes

Every command returns one of these, and each means the same thing whichever command returned it.

| Code | Meaning                             |
| ---- | ----------------------------------- |
| 0    | The command completed.              |
| 1    | The command ran and failed.         |
| 2    | `bv` refused the command line.      |
| 3    | A program `bv` invoked failed.      |
| 130  | The run was terminated with Ctrl-C. |

A command that returns 0 did what it was asked. What it found is its report's business: `bv dependencies show` returns 0 whatever the report says about the pins it lists.

Code 1 covers every failure that is `bv`'s own to state: a repository in a state the command cannot work with, a configuration file that cannot be read or validated, a file that cannot be read or written, and a resource `bv` needs and cannot reach, such as a package source.

A command that checks the repository rather than changing it returns 1 for what it found: `bv dependencies update --check` returns 1 when a pin has fallen behind its policy. Nothing failed there, and nothing was written. The repository is in a state the command exists to refuse, which is the same thing code 1 says everywhere else.

Code 2 is a refusal, not a failure: an unknown command, subcommand or option; an argument too many or too few; an option value that does not parse. Nothing the command would have done was done.

Code 3 covers `dotnet`, MSBuild, hooks, and any other program `bv` starts. The program either failed, or succeeded and produced output `bv` cannot use. The message names it and reports the exit code it chose. That code is never returned as `bv`'s own: it means whatever its author decided, and it would collide with the meanings above.

Code 130 is 128 + SIGINT, the POSIX convention for a process terminated by a signal.

One invocation returns a code `bv` did not choose, and no program `bv` started produced it. When the repository's tool manifest pins `bv`, the command line is delegated to the pinned version, whose exit code is returned as it stands. The delegated `bv` is the one that ran the command.
