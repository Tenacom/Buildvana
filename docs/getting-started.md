# Getting started

This page takes you from an empty repository to a first `bv build`.
Each step shows the command to run or the file to create, and says what Buildvana does with it.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Prerequisites](#prerequisites)
- [Create the repository](#create-the-repository)
- [Install `bv`](#install-bv)
- [Pin Buildvana SDK in `global.json`](#pin-buildvana-sdk-in-globaljson)
- [Create `buildvana.jsonc` and `VERSION`](#create-buildvanajsonc-and-version)
- [Import Buildvana SDK into the projects](#import-buildvana-sdk-into-the-projects)
- [Add a project and a solution](#add-a-project-and-a-solution)
- [Run the first build](#run-the-first-build)
- [Where to go next](#where-to-go-next)

---

## Prerequisites

- A .NET SDK at the minimum version that [Toolchain](introduction.md#toolchain) states, or a later one.
- Git.

Every command below runs in the directory you clone into, which becomes the [home directory](directory-structure.md#home-directory) of the repository.

---

## Create the repository

Create an empty repository on your Git server, clone it, and enter the directory:

```shell
git clone <url> MyProduct
cd MyProduct
dotnet new gitignore
```

`dotnet new gitignore` writes the `.gitignore` file of a .NET repository, and it lists `artifacts/` already.
Add one line for the scratch directory of `bv`:

```text
.buildvana-temp/
```

[`artifacts/`](directory-structure.md#artifacts) receives the results of a build, and [`.buildvana-temp/`](directory-structure.md#buildvana-temp) holds the temporary files of `bv`.

---

## Install `bv`

`bv` is a .NET tool.
Pin it in the tool manifest of the repository, so that every machine runs the same version:

```shell
dotnet new tool-manifest -o .config
dotnet tool install bv
```

The first command creates [`.config/dotnet-tools.json`](directory-structure.md#configdotnet-toolsjson), under `.config/`, where `bv` reads it.
The second adds `bv` to the manifest at the latest version.
From here on, `dotnet bv <command>` runs that version.
[Invocation](command-line.md#invocation) describes the other ways to run `bv`.

> [!NOTE]
> A preview version of Buildvana comes from the preview feed that the [README](../README.md) names.
> Before installing a preview, add the feed to a `nuget.config` file in the home directory, and pass `--prerelease` to `dotnet tool install`.

---

## Pin Buildvana SDK in `global.json`

Every project reads Buildvana SDK at the version that `global.json` pins.
`bv self-update` pins it at the version of the running `bv`, and creates the file when it is missing:

```shell
dotnet bv self-update
```

```text
bv: 2.1.538-preview (tool manifest, unchanged)
Buildvana.Sdk: 2.1.538-preview (global.json, added)
```

`global.json` then holds:

```json
{
  "msbuild-sdks": {
    "Buildvana.Sdk": "2.1.538-preview"
  }
}
```

`bv`, Buildvana SDK, and `Buildvana.Runtime` are released together, and [the SDK version check](command-line.md#the-sdk-version-check) keeps the two pins in step.
[Self-update](tool-commands/self-update.md) says what else the command updates.

---

## Create `buildvana.jsonc` and `VERSION`

`buildvana.jsonc` holds the settings of `bv` and Buildvana SDK.
The repository needs none yet, so the file holds the schema reference alone, with the pinned version in the URL:

```jsonc
{
  "$schema": "https://raw.githubusercontent.com/Tenacom/Buildvana/2.1.538-preview/schemas/buildvana.schema.json"
}
```

An editor reads the schema and completes the settings, and `bv self-update` moves the version in the URL with the other pins.
[The Buildvana configuration file](configuration-file.md) lists every setting.

`VERSION` states the version of the product, as `MAJOR.MINOR`:

```text
1.0
```

The [`Versioning` module](sdk-modules/versioning.md) computes the patch number from the Git history, so the file never holds one.

---

## Import Buildvana SDK into the projects

Two files in the home directory import Buildvana SDK into every project under it.
`Directory.Build.props` holds:

```xml
<Project>

  <Import Project="Sdk.props" Sdk="Buildvana.Sdk" />

</Project>
```

`Directory.Build.targets` holds:

```xml
<Project>

  <Import Project="Sdk.targets" Sdk="Buildvana.Sdk" />

</Project>
```

Neither import states a version, because `global.json` pins it.
The two files never change again, and the settings that every project shares go in `Common.props`.
Create it in the home directory, with one setting:

```xml
<Project>

  <PropertyGroup>
    <IncludeNuGetPackSupport>false</IncludeNuGetPackSupport>
  </PropertyGroup>

</Project>
```

`IncludeNuGetPackSupport` set to `false` turns off the `NuGetPack` module.
The module packs a README, a license, a third-party notice, and an icon into every package.
A packable project, such as a class library, fails to build until the four files exist.
Remove the setting when the library is ready to pack.
[`Common.props` and `Common.targets`](directory-structure.md#commonprops-and-commontargets) says what else goes in the file.

---

## Add a project and a solution

```shell
dotnet new classlib -o src/MyLibrary
dotnet new sln -n MyProduct
dotnet sln add src/MyLibrary
```

`bv` builds the solution file of the home directory, and takes a `.slnx` file before a `.sln` file.
Buildvana SDK enables StyleCop and the public API analyzers, and the template class `Class1.cs` raises warnings from both.
Delete `src/MyLibrary/Class1.cs`, and the empty library builds with no warning.

---

## Run the first build

Commit the files, then build:

```shell
git add -A
git commit -m "Set up the repository"
dotnet bv build
```

`bv build` cleans, restores, and builds the solution, and streams the output of `dotnet`:

```text
Buildvana CLI tool v2.1.538-preview

  Determining projects to restore...
  Restored C:\work\MyProduct\src\MyLibrary\MyLibrary.csproj (in 168 ms).
  MyLibrary -> C:\work\MyProduct\src\MyLibrary\bin\Release\net10.0\MyLibrary.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:01.25
```

The build configuration is `Release`, as [The build configuration](tool-commands/build-pipeline.md#the-build-configuration) says.
Buildvana computes version 1.0.1 for the build:

```shell
dotnet bv version show
```

```text
Buildvana CLI tool v2.1.538-preview

Current version:       1.0.1
Latest version:        (none)
Latest stable version: (none)
Public release:        no
Prerelease:            no
Current branch:        main
```

The commit that created `VERSION` gives the version line a Git height of 1, and every later commit adds one, as [Patch number](sdk-modules/versioning.md#patch-number) explains.

---

## Where to go next

- [Command line](command-line.md): the global options, delegation, the SDK version check, and the remedies for the most common failures.
- [Build pipeline](tool-commands/build-pipeline.md): what `bv clean`, `bv restore`, `bv build`, `bv test`, and `bv pack` do, and how to pass arguments to `dotnet`.
- [The Buildvana configuration file](configuration-file.md): every setting of `buildvana.jsonc`, with its default.
- [Directory structure](directory-structure.md): the recommended layout of the repository, and what each file is for.
- [`Versioning` module](sdk-modules/versioning.md): how the version of every project is computed, and how a branch produces public releases.
