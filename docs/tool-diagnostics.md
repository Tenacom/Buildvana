# Diagnostics and exit codes of `bv`

Every diagnostic `bv` raises has a `BV` prefix and a four-digit number from 1000 up.
This page lists them by range, and lists the exit codes every `bv` command returns.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Ranges](#ranges)
- [Main program (1000-1099)](#main-program-1000-1099)
- [Configuration (1100-1199)](#configuration-1100-1199)
- [Dependency management (1200-1299)](#dependency-management-1200-1299)
- [Exit codes](#exit-codes)

---

## Ranges

Each part of `bv` owns a range of 100 numbers, as the sections below list them.
The first range belongs to the main program.
A word in italics and parentheses, such as _(name)_, stands for a value the message carries.

---

## Main program (1000-1099)

The range holds no diagnostic.

---

## Configuration (1100-1199)

| Code   | Severity | Message                                               | Description                                                                                                                                 |
| ------ | :------: | ----------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| BV1100 |  Error   | _(the reason the JSON parser gives)_                  | The configuration file is not valid JSON. The message carries the reason of the parser, and the location points at the offending character. |
| BV1101 |  Error   | Expected _(type)_, but found _(type)_.                | A value has a type the schema does not allow at that location, such as a number where a string is required, or an explicit `null`.          |
| BV1102 |  Error   | _(value)_ is not one of the allowed values: _(list)_. | A value is not among those the schema permits at that location, such as an unknown enumeration value.                                       |
| BV1103 |  Error   | Unknown property '_(name)_'.                          | The file holds a property the schema does not define, or a dictionary key outside the allowed set.                                          |
| BV1104 |  Error   | Missing required property '_(name)_'.                 | A property the schema marks as required is absent.                                                                                          |
| BV1105 |  Error   | No value is allowed here.                             | A value appears where the schema permits none.                                                                                              |
| BV1106 |  Error   | The value must not be empty.                          | A string is shorter than the minimum length of the schema. For a required string, the member is stated and carries no value.                |
| BV1107 |  Error   | _(value)_ does not match the pattern '_(pattern)_'.   | A string does not match the pattern the schema requires. For a required string, the value is all whitespace.                                |
| BV1108 |  Error   | Duplicate property '_(name)_'.                        | An object states the same property name twice. The location points at the repeated name. Remove it, or merge the two into one property.     |

BV1106 and BV1107 also report a property name that carries data, as the member names of `dependencies.policies` and `dependencies.additionalPackages` do.
A name is held to the same rule as any other required string, and the location points at the name.

---

## Dependency management (1200-1299)

| Code   | Severity | Message                                                       | Description                                                                                                                                   |
| ------ | :------: | ------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| BV1200 |  Error   | No configured package source knows _(id)_.                    | No source the repository configures has ever had the package. The id is mistyped, or the source that has it is missing from `nuget.config`.   |
| BV1201 |  Error   | No configured package source has _(id)_ _(version)_.          | The sources know the package, and none has the version the repository pins, or the version `--to` states.                                     |
| BV1202 |  Error   | The .NET release index has no .NET SDK _(version)_.           | The version `global.json` pins, or the one `--to` states, is not a .NET SDK that Microsoft published.                                         |
| BV1203 |  Error   | No pin bv manages, in the selected scopes, has the id _(id)_. | `--to` states a version for an id that has no pin, or whose pins `bv` does not manage. A Buildvana family id is always this error.            |

BV1203 has a second message for an id of the Buildvana package family: `<id> belongs to Buildvana's own package family, which moves in lockstep. Use bv self-update.`
Those pins move together with Buildvana SDK, so no scope of `bv dependencies` manages one.

`bv dependencies update` reports every one of these, and each one stops the run before anything is written.
One run reports all of them, each naming the file that declares the pin.

---

## Exit codes

Every command returns one of these codes, and each one means the same thing whichever command returned it.

| Code | Meaning                             |
| ---- | ----------------------------------- |
| 0    | The command completed.              |
| 1    | The command ran and failed.         |
| 2    | `bv` refused the command line.      |
| 3    | A program `bv` invoked failed.      |
| 130  | The run was terminated with Ctrl-C. |

A command that returns 0 did what it was asked.
What it found is a matter for its report: `bv dependencies show` returns 0 whatever the report says about the pins.

Code 1 covers every failure that is `bv`'s own to state:

- a repository in a state the command cannot work with;
- a configuration file that cannot be read or validated;
- a file that cannot be read or written;
- a resource `bv` needs and cannot reach, such as a package source.

A command that checks the repository rather than changing it returns 1 for what it found.
`bv dependencies update --check` returns 1 when a pin has fallen behind its policy.
Nothing failed, and nothing was written.
The repository is in a state the command exists to refuse, which is what code 1 says everywhere else.

Code 2 is a refusal, not a failure.
It covers an unknown command, subcommand, or option, an argument too many or too few, and an option value that does not parse.
Nothing the command would have done was done.

Code 3 covers `dotnet`, MSBuild, hooks, and any other program `bv` starts.
The program failed, or succeeded and produced output `bv` cannot use.
The message names the program and reports its exit code.
`bv` never returns that code as its own, because its meaning belongs to the program, and it would collide with the codes above.

Code 130 is 128 plus SIGINT, the POSIX convention for a process terminated by a signal.

A delegated run returns a code `bv` did not choose.
When the tool manifest of the repository pins `bv`, the command line goes to the pinned version, whose exit code is returned as it stands.
The delegated `bv` is the one that ran the command.
