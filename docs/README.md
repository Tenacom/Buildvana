# Buildvana documentation

Every page of the Buildvana user documentation is listed here, in one group per topic, with one line saying what the page covers.

## Getting started

- [Introduction](introduction.md): what Buildvana is made of, what it does for a project, and what it runs on.
- [Getting started](getting-started.md): from an empty repository to a first `bv build`, one step at a time.

## Repository layout

- [Directory structure](directory-structure.md): the recommended layout of a repository that uses Buildvana SDK, and what each file and directory is for.
- [The Buildvana configuration file](configuration-file.md): where `bv` and Buildvana SDK find `buildvana.jsonc`, how a setting resolves, and every setting with its default.

## Buildvana SDK

- [Buildvana SDK configuration files](sdk-configuration-files.md): the machine-scoped and user-scoped `.props` files Buildvana SDK imports, and where it looks for them.
- [Syntax of constants in ThisAssembly classes](constants-syntax.md): how Buildvana SDK parses the `Value` metadata of a `ThisAssemblyConstant` item.
- [Internal-use properties](internal-use-properties.md): the `BV_` properties meant for use by Buildvana SDK and `bv` alone, and what each one means.
- [Diagnostics issued by Buildvana SDK](sdk-diagnostics.md): every `BVSDK` diagnostic, by module, with its severity and its meaning.

## SDK modules

- [`AdditionalAssemblyInfo` module](sdk-modules/additional-assembly-info.md): generating the `CLSCompliant` and `ComVisible` assembly attributes of a C# project.
- [`AlternatePack` module](sdk-modules/alternate-pack.md): publishing a project to folders and building Windows setup programs with Inno Setup, in place of NuGet packing.
- [`AssemblySigning` module](sdk-modules/assembly-signing.md): signing an assembly with a strong name from a `.pfx` certificate file.
- [`Dependencies` module](sdk-modules/dependencies.md): importing the transitive override files and dumping the package items of a project, both for `bv dependencies`.
- [`Hooks` module](sdk-modules/hooks.md): pinning `Buildvana.Runtime` to the version of Buildvana SDK in every file-based app of the repository.
- [`JetBrainsAnnotations` module](sdk-modules/jetbrains-annotations.md): exporting the JetBrains annotations of a C# project to a ReSharper external annotations file.
- [`NuGetPack` module](sdk-modules/nuget-pack.md): packing a README, a license, a third-party notice, and an icon into every NuGet package, and feeding a `.nuspec` file.
- [`ReleaseAssetList` module](sdk-modules/release-asset-list.md): writing the list of files that `bv release` uploads as the assets of a release.
- [`SourceGenerators` module](sdk-modules/source-generators.md): adding the Roslyn source generators of Buildvana SDK to a C# project, and checking that the compiler can run them.
- [`ThisAssemblyClass` module](sdk-modules/this-assembly-class.md): generating a `ThisAssembly` class holding constants that describe the assembly.
- [`Versioning` module](sdk-modules/versioning.md): computing the version of every project from a `VERSION` file and the Git history.
- [`Wine` module](sdk-modules/wine.md): running Windows-only build tools through Wine on Linux and macOS.

## `bv`

- [Command line](command-line.md): how to invoke `bv`, and what every command has in common, from the global options to delegation and the SDK version check.
- [Environment variables](environment-variables.md): every environment variable `bv` reads or sets.
- [Hooks](hooks.md): the repository-owned file-based apps `bv` runs at well-known events, and the args it passes them.
- [Diagnostics and exit codes of `bv`](tool-diagnostics.md): every `BV` diagnostic, and the exit codes every `bv` command returns.

## `bv` commands

- [Build pipeline](tool-commands/build-pipeline.md): `bv clean`, `bv restore`, `bv build`, `bv test`, and `bv pack`, the arguments all but `bv clean` forward to `dotnet`, and the build configuration.
- [Dependency management](tool-commands/dependencies.md): `bv dependencies`, its subcommands, its update policies, and the transitive overrides it writes.
- [Release](tool-commands/release.md): what `bv release` checks, what it does step by step, and what it undoes when a step fails.
- [Self-update](tool-commands/self-update.md): the pins `bv self-update` moves to one version, the summary it prints, and when it refuses to run.
- [Version](tool-commands/version.md): the report of `bv version show`, and the version spec change `bv version advance` applies.
