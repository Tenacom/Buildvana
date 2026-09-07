# Build pipeline

`bv clean`, `bv restore`, `bv build`, `bv test`, and `bv pack` are the build pipeline of `bv`.
This page says what each command does, how the arguments after `--` reach `dotnet`, and where the build configuration comes from.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [The pipeline](#the-pipeline)
- [`bv clean`](#bv-clean)
- [`bv restore`](#bv-restore)
- [`bv build`](#bv-build)
- [`bv test`](#bv-test)
- [`bv pack`](#bv-pack)
- [Forwarded arguments](#forwarded-arguments)
- [The build configuration](#the-build-configuration)
- [Exit codes](#exit-codes)

---

## The pipeline

The five commands are steps of one pipeline, in the order `clean`, `restore`, `build`, `test`, `pack`.
Each command runs the steps before it and then its own.
`bv build` cleans, restores, and builds, and `bv pack` runs all five steps.

Every step but `clean` wraps the `dotnet` command of the same name, run on the solution file of the home directory.
`bv` takes the first `.slnx` file of the home directory, or the first `.sln` file when there is none.
With neither, it fails.

Every `dotnet` command gets these arguments:

- `-nologo`;
- the build configuration, as [The build configuration](#the-build-configuration) says, except `dotnet restore`, which rejects it;
- the arguments of the `dotnet.all` section of `buildvana.jsonc`, then the arguments of the `dotnet.<command>` section, then the [forwarded arguments](#forwarded-arguments);
- `ContinuousIntegrationBuild=true` on GitHub Actions and GitLab CI, and `ContinuousIntegrationBuild=false` elsewhere;
- the verbosity of `bv`, as `--verbosity`.

The last two come last, so that no configured or forwarded argument overrides them.
Every `dotnet` command also gets the environment variables of the `dotnet.all` section, and then those of the `dotnet.<command>` section.
A `null` value removes the variable from the environment of the child.

`bv` sets no `-maxcpucount`, so MSBuild uses its default parallelism unless you forward `-m` or `-maxcpucount`.

The build pipeline commands observe [cancellation](../command-line.md#cancellation).
Every command but `clean` runs [the SDK version check](../command-line.md#the-sdk-version-check) first.

---

## `bv clean`

```text
bv clean [OPTIONS]
```

`bv clean` deletes every build artifact, intermediate output, and temporary file of the repository:

- `.vs\`, `_ReSharper.Caches\`, and `temp\` in the home directory;
- [`.buildvana-temp\`](../directory-structure.md#buildvana-temp), the scratch directory of `bv`;
- [`artifacts\`](../directory-structure.md#artifacts), the results of builds;
- `TestResults\` in the home directory, where [`bv test`](#bv-test) writes the test results;
- `bin\` and `obj\` of every project of the solution;
- the build cache of every [hook](../hooks.md#cleaning-hook-build-caches).

It leaves a `TestResults\` directory under a project alone.
`bv clean` runs no `dotnet` command, needs no SDK version check, and refuses a `--` separator, because it has nothing to forward to.
It does what `dotnet clean` does, and more.

---

## `bv restore`

```text
bv restore [-- <ARGS FORWARDED TO DOTNET>]
```

`bv restore` cleans, then runs `dotnet restore` on the solution, with `--disable-parallel`.

---

## `bv build`

```text
bv build [-- <ARGS FORWARDED TO DOTNET>]
```

`bv build` cleans, restores, then runs `dotnet build` on the solution, with `--no-restore`.

---

## `bv test`

```text
bv test [-- <ARGS FORWARDED TO DOTNET>]
```

`bv test` cleans, restores, builds, then runs the tests of the solution.

Microsoft.Testing.Platform is the only test runner `bv` supports.
A test project is a project whose `IsTestingPlatformApplication` property is `true`.
The test frameworks built on Microsoft.Testing.Platform set the property through their SDK, so a project rarely sets it itself.
A project whose name ends in `.Tests` is not a test project for that reason, and `bv` does not read `IsTestProject`, the property of VSTest.
A VSTest project fails at test time.
Buildvana SDK reads the same property, and sets [`BV_IsTestProject`](../internal-use-properties.md#project-type) from it.

`bv test` first asks MSBuild for the property of every project of the solution, in turn, and stops at the first test project.
When the solution has none, `bv test` prints `notice: No test projects found, skipping tests.` and returns 0.
Otherwise it runs `dotnet test` on the solution, with `--no-restore` and `--no-build`, and with these arguments:

- `--results-directory TestResults`, so that every report goes to `TestResults\` in the home directory;
- `--output Detailed` at the `detailed` verbosity and above, and `--output Normal` below it.

Each test project writes its own reports, under its own name, and `bv` merges none of them.
A coverage report is one of those reports, when the test run asks for coverage.
This repository asks for it in the `dotnet.test` section of its `buildvana.jsonc`, with the arguments `--coverage --coverage-output-format cobertura`.
A tool that consumes coverage, such as the Codecov action, then reads several `*.cobertura.xml` files, which the action accepts as a glob.

---

## `bv pack`

```text
bv pack [-- <ARGS FORWARDED TO DOTNET>]
```

`bv pack` cleans, restores, builds, tests, then runs `dotnet pack` on the solution, with `--no-restore` and `--no-build`.
The packages go to `artifacts\<configuration>\`, such as `artifacts\Release\`.
Buildvana SDK may make the `Pack` target produce other artifacts, such as setup executables, in the same directory.

---

## Forwarded arguments

Everything after the first `--` is forwarded to `dotnet` verbatim, in the order given.
`bv` neither parses nor validates it, with one exception, the build configuration, described [below](#the-build-configuration).
The forwarded arguments reach every `dotnet` command of the run.
`bv pack -- -m:8` passes `-m:8` to `dotnet restore`, `dotnet build`, `dotnet test`, and `dotnet pack` alike.

A malformed or unknown forwarded argument is an error from `dotnet`, or from the test application under `dotnet test`, and not from `bv`.

Before `--`, a build pipeline command accepts the global options and nothing else.
A token that looks like an option, or an argument, is refused with exit code 2:

```text
error: Unexpected argument '-m:8' for command 'build'. Forward arguments to dotnet after '--', e.g. 'bv build -- -m:8'.
```

---

## The build configuration

`bv` needs the build configuration to pass it to `dotnet`, and to find the artifacts.
It takes the first of these that states one:

1. `-c` or `--configuration` among the forwarded arguments, as `bv pack -- -c Debug`;
2. the `dotnet.configuration` setting of `buildvana.jsonc`;
3. `Release`.

`bv` owns `-c` and `--configuration` wherever they appear after `--`.
It reads the value, strips both names from what reaches `dotnet`, and passes the configuration itself, in the form each command accepts.
`dotnet build` and `dotnet pack` take `-p:Configuration=`, `dotnet test` takes `--property:Configuration=`, and `dotnet restore` takes none.
Left in, the option would fail every run at the restore step, because `dotnet restore` rejects `-c`.

Owning the two names has two consequences.
A trailing `-c` or `--configuration` with no value after it is an error from `bv`.
A `-c` meant as the value of another forwarded option is read as the configuration.

`bv release` takes `-c` as an [option of its own](release.md#options), because it needs the value to find the artifacts.

---

## Exit codes

The build pipeline commands return the [exit codes every `bv` command returns](../tool-diagnostics.md#exit-codes).

Code 1 is a failure of `bv` before any `dotnet` command runs.
No home directory, no solution file, a `buildvana.jsonc` that does not load, and a failed SDK version check are the causes.
Code 2 is a refusal of the command line: an option `bv` does not know before `--`, or an argument there.
Code 3 says that a `dotnet` command failed, and the message reports its exit code.
A build error and a failed test both end there, because `dotnet build` and the test application report them through their exit codes.
Code 130 is a run terminated with Ctrl-C.
