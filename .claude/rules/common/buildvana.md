# Buildvana

This file is the same in every repository that copies `.claude`, and it assumes a repository built with Buildvana SDK and `bv`. A fact specific to one repository lives in another rule file.

Buildvana is the build system of this repository. It has two parts. Buildvana SDK is an MSBuild SDK that works alongside the SDK a project specifies. `bv` is a .NET local tool that runs the build pipeline and other build operations. Both work from the home directory, which is usually the root of the repository.

## Documentation

The Buildvana documentation lives in the `docs/` folder of [Tenacom/Buildvana](https://github.com/Tenacom/Buildvana/tree/main/docs). The links in this file point at `main`, which describes the latest `bv`.

- Read the documentation of the `bv` version this repository pins. The `tools.bv.version` property of `dotnet-tools.json` holds it.
- Each release has a tag named after the bare version, such as `2.1.768-preview`. Replace `main` with that tag in a link: `https://github.com/Tenacom/Buildvana/blob/<version>/docs/...`.
- Before answering a question about the behavior of `bv` or Buildvana SDK, read the documentation page about it. Do not answer from memory.

## Build commands

Run `dotnet tool restore` first, to install the local tools. Then use these commands:

- `dotnet bv clean`: remove all build artifacts, intermediate output, and temporary files.
- `dotnet bv restore`: restore NuGet packages.
- `dotnet bv build`: build the solution.
- `dotnet bv test`: run the tests.
- `dotnet bv pack`: create the NuGet packages in `artifacts/`.

Each command includes the ones before it: `dotnet bv build` also cleans and restores, `dotnet bv test` also builds, and so on. [Build pipeline](https://github.com/Tenacom/Buildvana/blob/main/docs/tool-commands/build-pipeline.md) documents them.

### Capturing build/test output

`bv` streams a large amount of child-process output line by line. The agent's shell tools truncate that output **before the final summary**, the `Build succeeded` line and the warning and error counts. So the outcome of `dotnet bv build` or `dotnet bv test` is not readable from the tail.

To verify a build, run a plain `dotnet build` through PowerShell from the home directory, and keep only the tail:

```powershell
dotnet build -v m | Select-Object -Last 25
```

Without a file argument, `dotnet build` builds the one solution file in the current directory.

This command shows the per-project outputs plus the summary. Use `dotnet bv build`, `pack`, or `test` when you need the full clean, restore, and build chain, or the artifacts. Use the direct `dotnet build` above for a quick compile-and-warning check.

## Dependencies

[Dependency management](https://github.com/Tenacom/Buildvana/blob/main/docs/tool-commands/dependencies.md) documents `bv dependencies`, its scopes, its update policies, and what `bv` counts as a pin. `bv deps` is an alias.

### Where versions live

- `Directory.Packages.props` holds the central package versions. It has one `ItemGroup` per kind of dependency:
  - run-time dependencies
  - development dependencies
  - test dependencies, when present
  - versions that override transitive dependencies, when present
- `global.json` holds the .NET SDK version and the version of Buildvana SDK.
- `dotnet-tools.json` holds the versions of the local tools, `bv` included.
- Buildvana SDK adds some packages to every project. [What Buildvana SDK contributes](https://github.com/Tenacom/Buildvana/blob/main/docs/tool-commands/dependencies.md#what-buildvana-sdk-contributes) lists them.

### How to update dependencies

Run `dotnet bv deps update` from the home directory. One run moves each pin it manages as far as its update policy allows.

- `dotnet bv deps update --check` reports what a run would do, and writes nothing. It exits 1 when anything would move. Do not read that exit code as a failure.
- Add `--all` to `--check` to list every pin, not only the ones that would move. In the output, the line of each pin ends with the latest stable version the sources have. The latest prerelease above it follows, when there is one.
- `dotnet bv deps update <id> --to <version>` moves every pin of that id to the version, whatever its policy. It is the one way to lower a pin.
- `dotnet bv deps update <id or pattern>... --latest` moves the named pins to the latest version the sources have, whatever their policy.
- Move packages that depend on each other at one version, such as `Microsoft.CodeAnalysis.*`, in one `--latest` run. With one `--to` run per package, the restore of the transitive overrides fails until the last run.
- `bv deps` knows nothing about a package with no pin. To find its latest version, run `dotnet package search <id> --exact-match`. Add `--prerelease` to count prereleases as well.
- Do not update tools with `dotnet tool update --local --all`. For a tool pinned to a prerelease line, the command picks the latest stable version, which is a downgrade. The command then fails the whole run instead of downgrading. Update each tool with `dotnet tool update <id> --local --version <version>` instead.

### The Buildvana family

`bv`, `Buildvana.Sdk`, and `Buildvana.Runtime` form the Buildvana family. They are released together, and a repository pins them at one version. `bv deps` does not move them. [`bv self-update`](https://github.com/Tenacom/Buildvana/blob/main/docs/tool-commands/self-update.md) moves all of them at once.

## Other commands

- `dotnet bv version` reports the version of the repository, and writes nothing. Run it whenever you need the version.
- Do not run the following commands unless I ask:
  - `dotnet bv version advance` changes the version specification in `VERSION`.
  - `dotnet bv self-update` moves the pins of the Buildvana family.
- Never run `dotnet bv release`. It publishes a release, and it runs only on a CI runner.

[Version](https://github.com/Tenacom/Buildvana/blob/main/docs/tool-commands/version.md) and [Release](https://github.com/Tenacom/Buildvana/blob/main/docs/tool-commands/release.md) document `bv version` and `bv release`.
