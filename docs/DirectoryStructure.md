# Directory structure

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Overview](#overview)
- [Home directory](#home-directory)
  - [Location of the home directory](#location-of-the-home-directory)
- [`.buildvana-temp\`](#buildvana-temp)
- [`.config\dotnet-tools.json`](#configdotnet-toolsjson)
- [`artifacts\`](#artifacts)
- [`src\`, `tests\`, `samples\`](#src-tests-samples)
- [`Common.props` and `Common.targets`](#commonprops-and-commontargets)
- [`Directory.Build.props` and `Directory.Build.targets`](#directorybuildprops-and-directorybuildtargets)
- [`global.json`](#globaljson)
- [`LICENSE`](#license)
- [`README.md`](#readmemd)
- [`THIRD-PARTY-NOTICES`](#third-party-notices)
- [`VERSION`](#version)

## Overview

This is the recommended directory structure for a repository using Buildvana SDK.

The asterisk `(*)` marks files and directories that are always present. Other files and directories may or may not be present, depending on the specific project; for example, not all projects need a `lib` subdirectory.

We will follow the MSBuild convention of a backslash (`\`) as a path separator. On non-Windows systems, MSBuild automatically converts backslashes to slashes (`/`) when accessing the filesystem.

```text
<some_path>\                   <<< (*) Home directory (the root of your repository)
|
+--- .buildvana\               <<< Optional grouping directory for Buildvana files
|    |
|    +--- hooks\               <<< Repo-owned hooks run by bv (see Hooks.md)
|    |    |
|    |    +--- release\
|    |         |
|    |         +--- post-release.cs
|    |
|    +--- buildvana.jsonc      <<< Buildvana configuration file, if not in the home directory root
|
+--- .buildvana-temp\          <<< bv's scratch directory (machine-generated; add to .gitignore)
|
+--- .config\
|    |
|    +--- dotnet-tools.json    <<< .NET local tool manifest; pins the bv version used by `dotnet bv`
|
+--- artifacts\                <<< (*) Final results of builds
|
+--- samples\                  <<< Sample projects
|    |
|    +--- Common.props         <<< Portions of MSBuild code common to all projects in samples\
|    +--- Common.targets
|
+--- src\                      <<< (*) Source code (except tests and sample projects)
|    |
|    +--- Common.props         <<< Portions of MSBuild code common to all projects in src\
|    +--- Common.targets
|
+--- tests\                    <<< Test projects
|    |
|    +--- Common.props         <<< Portions of MSBuild code common to all projects in tests\
|    +--- Common.targets
|
+--- buildvana.jsonc           <<< Buildvana configuration file (or buildvana.json), if not in .buildvana\
|
+--- Common.props              <<< Common parts of MSBuild projects
+--- Common.targets
|
+--- Directory.Build.props     <<< (*) Scaffold files used to import Buildvana SDK
+--- Directory.Build.targets
|
+--- global.json               <<< (*) Pins the Buildvana SDK version (and, optionally, the .NET SDK version)
|
+--- LICENSE                   <<< License file
|
+--- README.md                 <<< README file
|
+--- THIRD-PARTY-NOTICES       <<< Third-party copyright notices
|
+--- VERSION                   <<< (*) Single source of truth for project version
|
+--- <solution>.sln            <<< (*) Your solution file
```

This document explains what each of this files and directories is and how it is related to Buildvana SDK.

## Home directory

This is the "home" of your product. All files specific to your product should be here, or in a directory herein; once this directory is copied to another computer, as long as it has the right tools installed, the product may be built on the second computer exactly the same way as on the first.

This is also, usually, the root of your repository: it is where you checked out to, or checked in from. A Git repository is not strictly required, though: any directory containing a Buildvana configuration file can serve as a home directory (see below).

The full path of the home directory, including a trailing path separator, is stored in the `HomeDirectory` MSBuild property. You can use this property to define your own paths as needed. For example:

```XML
  <!-- Directory where I keep some additional files I need. -->
  <PropertyGroup>
    <!-- The HomeDirectory property is guaranteed to end with a path separator. -->
    <MyDirectory>$(HomeDirectory)MyStuff\</MyDirectory>
  </PropertyGroup>
```

**Note for Windows users:** Do not nest a home directory too deeply in a drive, as Windows has a 260-character limitation on the length of paths (you can read more about it in [this article](https://docs.microsoft.com/en-us/windows/win32/fileio/naming-a-file#maximum-path-length-limitation) on Microsoft's documentation site.) There are bound to be some levels of nested directories under the home directory: for example, the executable file for a project might be `$(HomeDirectory)\src\MyProgram\bin\Release\netcoreapp3.1\MyProgram.exe`. If the `$(HomeDirectory)` part is more than 200 characters long to start with, the compiler won't even be able to create the executable.

### Location of the home directory

Buildvana SDK determines the location of the home directory by walking up the directory hierarchy, starting from the project's directory (included), and stopping at the nearest directory that contains any of these home markers:

- a Buildvana configuration file (`buildvana.json` or `buildvana.jsonc`), either directly in the directory or in a `.buildvana` subdirectory (a `.buildvana` directory without a configuration file is _not_ a marker);
- a Git worktree or submodule (a file named `.git`);
- a regular Git repository (a file named `HEAD` in a `.git` subdirectory).

The directory containing the marker becomes the home directory, and its full path becomes the value of `HomeDirectory`. Note that a configuration file inside `.buildvana` marks the directory containing `.buildvana`, not `.buildvana` itself. A configuration file does not have to actually configure anything: an empty JSON object (`{}`) is valid content, making the file usable as a pure home-directory marker.

If no marker is found, the build (or project loading in Visual Studio) stops with error [BVSDK1003](SdkDiagnostics.md#buildvana-sdk-core-1000-1049).

## `.buildvana-temp\`

bv's scratch directory: machine-generated temporary files, such as the args files for [hooks](Hooks.md#the-hook-args), live here. Add it to `.gitignore`: `bv` itself never considers its contents when detecting working-tree changes during a release, but without the ignore entry, Git tooling will show them as untracked. `bv clean` deletes the directory.

## `.config\dotnet-tools.json`

[`dotnet-tools.json`](https://learn.microsoft.com/en-us/dotnet/core/tools/local-tools-how-to-use) is the .NET local tool manifest: it pins the versions of the .NET tools used by the repository, so that `dotnet <tool>` invocations run the pinned versions. In a repository using Buildvana, this usually includes `bv` itself, which is why the manifest appears in the directory structure above. It is optional, though: `bv` can also be installed globally, or run via `dnx`, in which case the manifest (or its `bv` entry) may be absent.

Besides being read by the .NET CLI itself, the manifest drives `bv`'s _delegation_: whenever it pins `bv`, the pinned version is the one that runs, no matter which `bv` you invoke — like the Angular CLI, where a global `ng` always hands over to the project-local install. On every invocation, `bv` reads the manifest's `bv` entry and, unless it is itself the pinned version running from the local tool cache, delegates the entire command line to the pinned version: it makes sure the version is installed — probing the same cache `dotnet tool run` resolves tools from, and running `dotnet tool restore` only on a miss; a failed restore is reported but does not block the attempt — then runs it (`dotnet tool run bv`) with inherited standard streams, and forwards its exit code. When the versions differ, an info line on standard error names the version that runs:

```text
Delegating to bv 2.1.58-preview from this repository's tool manifest.
```

A delegating `bv` does not judge the command line beyond the minimal split that finds the subcommand and the global options, and does not read the configuration file at all: both may be valid for the pinned version and not for the invoked one, and judging them is the pinned version's job. The split does reject one malformed shape on its own — a value-bearing global option with nothing after it, such as a trailing `-v` — but every `bv` version phrases that rejection the same way, so the answer does not depend on which binary gives it. There are two exceptions to the hand-over itself:

- the `--skip-delegation` global option runs the exact binary you invoked;
- the [`update`](#globaljson) subcommand always runs the invoked `bv`, since its job is precisely to re-pin the repository to that `bv`'s version.

Two details of the hand-over are worth knowing. The delegated `bv` runs from the home directory, not from the directory you invoked it in, so that the .NET CLI resolves this repository's manifest rather than a nested one; `bv`'s own arguments are unaffected — they are interpreted against the home directory anyway — but a relative path inside _forwarded_ arguments (e.g. `bv build -- -p:SomeDir=../out` from a subdirectory) is interpreted by the pinned `bv` from the home directory, not from where you typed it. And `--version` answers for the `bv` that actually runs — the pinned one, consistent with the rest of the invocation; pass `--skip-delegation --version` to ask the exact binary you invoked.

An [environment variable](EnvironmentVariables.md), `BV_DELEGATED`, is set on the delegated `bv` (carrying the delegating `bv`'s version) so that a delegated invocation never delegates again.

## `artifacts\`

This is where the results of your hard work will be stored, in the form of NuGet packages, setup executables, ready to-deploy web directories, and so on.

Buildvana SDK will automatically create this directory if it does not exist.

## `src\`, `tests\`, `samples\`

The only hard rule about the location of projects in a product uising Buildvana SDK is that they must reside somewhere under `HomeDirectory`.

The following three locations are strongly recommended, though:

- `src\` for the product itself;
- `tests\` for test projects;
- `samples\` for sample projects.

```text
<home_directory>\
|
+--- samples\
|    |
|    +--- Sample1\                <<< Sample project to illustrate use of MyLibrary
|    |    |
|    |    +--- Sample1.csproj
|    |    +--- ...
|    |
|    +--- Sample2\                <<< Another sample project
|    |    |
|    |    +--- Sample2.csproj
|    |    +--- ...
|    |
|    +--- Common.props
|    +--- Common.targets
|
+--- src\
|    |
|    +--- MyLibrary\              <<< My library (probably distributed as a NuGet package)
|    |    |
|    |    +--- MyLibrary.csproj
|    |    +--- ...
|    |
|    +--- MyLibrary.Extras\       <<< Additional features for MyLibrary (distributed as a separate package)
|    |    |
|    |    +--- MyLibrary.Extras.csproj
|    |    +--- ...
|    |
|    +--- Common.props
|    +--- Common.targets
|
+--- tests\
|    |
|    +--- MyLibrary.Tests\        <<< Unit tests for MyLibrary
|    |    |
|    |    +--- MyLibrary.Tests.csproj
|    |    +--- ...
|    |
|    +--- MyLibrary.Extras.Tests\  <<< Unit tests for MyLibrary.Extras
|    |    |
|    |    +--- MyLibrary.Extras.Tests.csproj
|    |    +--- ...
|    |
|    +--- Common.props
|    +--- Common.targets
|
+--- MyLibrary.sln
+--- ...
```

The advantages of grouping similar projects under subdirectories become evident when you start to put common parts of projects (such as common dependencies) in `Common.props` and `Common.targets` files. This is explained below [in its own section](#commonprops-and-commontargets).

## `Common.props` and `Common.targets`

You may be aware of how MSBuild [automatically imports](https://docs.microsoft.com/visualstudio/msbuild/customize-your-build#directorybuildprops-and-directorybuildtargets) `Directory.Build.props` and `Directory.Build.targets` files. You can use them to define common properties, build settings, and the like, for all projects residing under a directory.

This method, however, has an annoying limitation: MSBuild will only import the _first_ `Directory.Build.props` (or `Directory.Build.targets`) file it finds, looking from the project's directory and going up the hierarchy.

Say you have both a `Directory.Build.props` file in the home directory and one in the `src\` subdirectory: only the latter will be "seen" by MSBuild, unless you add code in it to explicitly import the other. Although not a big burden at the beginning, this may easily lead to confusion, as the necessary `<Import>` tag mey be at the beginning, at the end, or even in the middle of the file, making it difficult for new collaborators to understand which file's `<PropertyGroup>`s may override those in other files.

Even if you have no such files in your repository, but there is a `Directory.Build.props` and/or `Directory.Build.targets` file in a directory above it, they will be silently imported, potentially altering your build process in unpredictable ways.

**Buildvana SDK discourages the use of `Directory.Build.props` and/or `Directory.Build.targets` files** in favor of `Common.props` and `Common.targets`, respectively.

`Common.props` and `Common.targets` files serve the same purpose as MSBuild's `Directory.Build.props` and `Directory.Build.targets`: specify information and/or build istructions that are common to all projects contained in a directory or in a subdirectory therein. Such information may include, for example, properties such as `<Owners>`, `<Company>`, `<Copyright>`... all that redundant stuff that is the same for all related projects.

The advantage of `Common.*` versus `Directory.Build.*` files is predictability. Buildvana SDK will import `Common.*`  files starting from the home directory and moving down to the project's directory; therefore, settings (e.g. property values) specified in a directory may be overridden in a subdirectory. Furthermore, `Common.*` files external to the repository will never be imported.

A typical `Common.props` file in a home directory may look like this:

```XML
<Project>

  <!-- Common project / package metadata -->
  <PropertyGroup>
    <Product>MyProduct</Product>
    <Authors>myself</Authors> <!-- My NuGet account -->
    <Owners>mycompany</Owners> <!-- The company's NuGet account, used to upload packages -->
    <Company>MyCompany, Inc.</Company>
    <Copyright>Copyright (C) 2018-2020 MyCompany, Inc.</Copyright>
    <PackageReleaseNotes>A changelog is available at $(PackageProjectUrl)/blob/master/CHANGELOG.md</PackageReleaseNotes>
  </PropertyGroup>

</Project>
```

An example `tests\Common.props` file may look like this:

```XML
<Project>

  <PropertyGroup>
    <TargetFramework>netcoreapp3.1</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="nunit" />
    <PackageReference Include="NUnit3TestAdapter" />
  </ItemGroup>

</Project>
```

(You may have noticed that no version is specified for package references. This assumes that you are [managing package versions centrally](https://stu.dev/managing-package-versions-centrally/), which is one of the good practices contemplated by the Buildvana method.)

`Common.targets` files are not so often needed as their `.props` counterparts. They may contain, for example, [BeforeBuild and/or AfterBuild targets](https://docs.microsoft.com/en-us/visualstudio/msbuild/how-to-extend-the-visual-studio-build-process) that you want to add to all projects, or at least to all projects within a directory.

## `Directory.Build.props` and `Directory.Build.targets`

These two files are the only exception to the "no-Directory.Build-files" rule outlined [in the previous section](#commonprops-and-commontargets).

These files, which must be in the home directory, serve two purposes:

- importing `Sdk.props` and `Sdk.targets`, respectively, from Buildvana SDK's NuGet package, and
- making sure that no other `Directory.Build.*` file from outside the repository is imported.

Here's what must be in `Directory.Build.props`:

```XML
<Project>

  <Import Project="Sdk.props" Sdk="Buildvana.Sdk" /> <!-- Buildvana.Sdk version is specified in global.json -->

</Project>
```

As you may have guessed, `Directory.Build.targets` is similar:

```XML
<Project>

  <Import Project="Sdk.targets" Sdk="Buildvana.Sdk" /> <!-- Buildvana.Sdk version is specified in global.json -->

</Project>
```

Note that neither `<Import>` carries a `Version` attribute: the version of Buildvana SDK is pinned once, for the whole repository, in [`global.json`](#globaljson). Pinning the version in a single place keeps the two files identical across repositories and guarantees that `Sdk.props` and `Sdk.targets` are imported from the same version of Buildvana SDK. Should they ever come from different versions — for example, because of stray `Version` attributes — they might be incompatible with each other; Buildvana SDK detects such a situation and issues a [`BVSDK1002`](SdkDiagnostics.md#buildvana-sdk-core-1000-1049) error.

It is important that no other `Directory.Build.props` and / or `Directory.Build.targets` files exist in the repository; use `Common.props` and `Common.targets`, instead, as explained above.

## `global.json`

[`global.json`](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json) is where the .NET SDK looks up the version of any MSBuild project SDK referenced without an explicit version, under the `msbuild-sdks` key. Since the `<Import>` elements in `Directory.Build.props` and `Directory.Build.targets` reference Buildvana SDK without a `Version` attribute (see [the previous section](#directorybuildprops-and-directorybuildtargets)), the version of Buildvana SDK used by the repository is pinned here:

```JSON
{
  "msbuild-sdks": {
    "Buildvana.Sdk": "1.0.0"
  }
}
```

Of course, `global.json` can also serve its better-known purpose, pinning the version of the .NET SDK itself via the `sdk` key; the two uses coexist in the same file.

The pinned version is not just a build input: `bv`, Buildvana SDK, and the `Buildvana.Runtime` library are released in lockstep and designed to work as a matched group. Every `bv` command that uses the SDK (`restore`, `build`, `test`, `pack`, and `release`) first verifies that the pinned version matches the version of the running `bv`, and refuses to run on a mismatch — including a missing `global.json`, section, or entry (pass `--skip-sdk-check` to bypass the check when you need a deliberate mismatch). Thanks to [delegation](#configdotnet-toolsjson), the `bv` that runs is normally the one pinned in the tool manifest, so the check can only trip when the repository's own pins disagree with each other — a half-updated repository.

To update the repository as a whole, run `bv update`: it re-pins the repository's entire Buildvana surface — the `bv` entry in the [tool manifest](#configdotnet-toolsjson), the `Buildvana.Sdk` entry in `global.json`, and the [configuration file](ConfigurationFiles.md)'s `$schema` reference — to the version of the running `bv`, creating files and sections as needed and preserving formatting everywhere. The tool manifest is updated through `dotnet tool update` (or `dotnet tool install --create-manifest-if-needed`), which also downloads the version so the next `dotnet bv` invocation can run it; afterwards, the configuration file is loaded with the new version's model, and any problems are reported as warnings for you to review.

`update` is exempt from delegation — it updates the repository to the `bv` you actually invoked ("bring this repository to me"). The usual upgrade flow is therefore: update your global `bv` (`dotnet tool update -g bv`), then run `bv update` in the repository; `dnx bv@<version> update` targets any specific version without touching the global install. As a safety net, `bv update` refuses to move a repository backwards when its pins are newer than the running `bv`, unless you pass `--force`.

## `LICENSE`

TODO

## `README.md`

TODO

## `THIRD-PARTY-NOTICES`

TODO

## `VERSION`

The single source of truth for the version of your product: a plain-text file, in the home directory, holding a single `MAJOR.MINOR[-[tag]]` version specification, for example:

```text
2.0-preview
```

The presence of `-` after the minor version marks a prerelease line; the tag text after it is optional and informational (the effective prerelease tag comes from the `versioning.prereleaseTag` key of `buildvana.json`). The patch number is not stored in the file: it is the Git height of the version line, i.e. the number of commits since the last change of `MAJOR.MINOR`.

The height restarts from 1 whenever `MAJOR.MINOR` changes, and a `VERSION` file that did not exist before counts as a change: the first commit on a new version line always computes patch 1, no matter how much history precedes it. Therefore, **adopt Buildvana — or upgrade from a Buildvana version that read `version.json` — in the same commit that bumps `MAJOR.MINOR`**. On a fresh version line the restart costs nothing; stay on the old line instead, and every version computed afterwards is lower than the ones already published on it, until the line accumulates more commits than the highest patch number you published. `bv release` refuses to publish a version lower than your latest release tag, so the mistake blocks a release rather than corrupting your feed, but recovering from it takes a commit: edit `MAJOR.MINOR` in `VERSION` and commit that before releasing again. Passing `--bump` to `bv release` does not help, because the check runs before the requested bump is applied.

Computing the height requires the full commit history. A shallow clone — `git clone --depth`, or a CI checkout that does not ask for everything, such as an `actions/checkout` step without `fetch-depth: 0` — sees fewer commits and therefore computes a lower patch number. Take particular care on a release build: a shallow fetch usually brings down no tags either, leaving `bv release` with no previous release to compare against, so the check just described has nothing to catch.

When a `VERSION` file is present, the Buildvana SDK computes `Version`, `AssemblyVersion`, `FileVersion`, and `InformationalVersion` for all projects in the repository; `bv` uses the same computation for releases and rewrites the file when advancing the version.
