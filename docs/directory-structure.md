# Directory structure

This page describes the recommended layout of a repository that uses Buildvana SDK, and says what each file and directory is for.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Layout](#layout)
- [Home directory](#home-directory)
  - [Location of the home directory](#location-of-the-home-directory)
- [`.buildvana-temp\`](#buildvana-temp)
- [`dotnet-tools.json`](#dotnet-toolsjson)
  - [Migration from `.config\dotnet-tools.json`](#migration-from-configdotnet-toolsjson)
- [`artifacts\`](#artifacts)
- [`src\`, `tests\`, `samples\`](#src-tests-samples)
- [`Common.props` and `Common.targets`](#commonprops-and-commontargets)
- [`Directory.Build.props` and `Directory.Build.targets`](#directorybuildprops-and-directorybuildtargets)
- [`global.json`](#globaljson)
- [`VERSION`](#version)

---

## Layout

The asterisk, `(*)`, marks the files and directories that every repository has.
The others depend on the product.
The tree follows the MSBuild convention of a backslash as the path separator.
On a system other than Windows, MSBuild turns a backslash into a slash when it accesses the file system.

```text
<some_path>\                   <<< (*) Home directory, usually the root of the repository
|
+--- .buildvana\               <<< Grouping directory for Buildvana files
|    |
|    +--- hooks\               <<< Hooks, run by bv (see hooks.md)
|    |    |
|    |    +--- deps\
|    |    |    |
|    |    |    +--- post-update.cs
|    |    |
|    |    +--- release\
|    |         |
|    |         +--- post-release.cs
|
+--- .buildvana-temp\          <<< Scratch directory of bv (machine-generated; add to .gitignore)
|
+--- artifacts\                <<< (*) Results of builds
|
+--- samples\                  <<< Sample projects
|    |
|    +--- Common.props         <<< MSBuild code common to every project under samples\
|    +--- Common.targets
|
+--- src\                      <<< (*) Source code, except tests and sample projects
|    |
|    +--- Common.props         <<< MSBuild code common to every project under src\
|    +--- Common.targets
|
+--- tests\                    <<< Test projects
|    |
|    +--- Common.props         <<< MSBuild code common to every project under tests\
|    +--- Common.targets
|
+--- buildvana.jsonc           <<< Buildvana configuration file (or buildvana.json)
|
+--- Common.props              <<< MSBuild code common to every project
+--- Common.targets
|
+--- Directory.Build.props     <<< (*) Scaffold files that import Buildvana SDK
+--- Directory.Build.targets
|
+--- dotnet-tools.json         <<< .NET local tool manifest; pins the bv version that `dotnet bv` runs
|
+--- global.json               <<< (*) Pins the Buildvana SDK version, and optionally the .NET SDK version
|
+--- LICENSE                   <<< License file, packed by the NuGetPack module
|
+--- README.md                 <<< README file, packed by the NuGetPack module
|
+--- THIRD-PARTY-NOTICES       <<< Third-party copyright notices, packed by the NuGetPack module
|
+--- VERSION                   <<< (*) Version of the product
|
+--- <solution>.slnx           <<< (*) Solution file, in either format
```

---

## Home directory

The home directory holds every file of the product.
A copy of the directory on another computer with the same tools builds the product the same way.

The home directory is usually the root of the repository: the directory you cloned into.
A Git repository is not required, because any directory that holds `buildvana.jsonc` can serve as a home directory, as [Location of the home directory](#location-of-the-home-directory) says.

The `HomeDirectory` MSBuild property holds the full path of the home directory, with a trailing path separator.
A path of yours can build on it:

```xml
<PropertyGroup>
  <MyDirectory>$(HomeDirectory)MyStuff\</MyDirectory>
</PropertyGroup>
```

> [!NOTE]
> On Windows, keep the home directory near the root of a drive.
> A path is limited to 260 characters, as [Maximum Path Length Limitation](https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation) explains.
> An output path such as `$(HomeDirectory)src\MyProgram\bin\Release\net10.0\MyProgram.exe` adds several levels, and a home directory over 200 characters leaves the compiler no room for it.

### Location of the home directory

Buildvana SDK walks up from the directory of the project, that directory included, and stops at the nearest directory that holds a home marker:

- [`buildvana.jsonc`](configuration-file.md), under either name;
- a Git worktree or submodule, as a file named `.git`;
- a Git repository, as a file named `HEAD` in a `.git` subdirectory.

That directory becomes the home directory, and `HomeDirectory` holds its full path.
A marker sits in the directory it marks, so nothing under a subdirectory takes part in discovery, `.buildvana\` included.
A hook is a project under `.buildvana\`, and a marker recognized there would make each hook discover `.buildvana\` as its home directory.
A configuration file does not have to configure anything: an empty JSON object, `{}`, is valid content, and marks the directory.

When no marker exists, the build stops with error [BVSDK1003](sdk-diagnostics.md#buildvana-sdk-core-1000-1049).
Loading the project in Visual Studio stops the same way.

---

## `.buildvana-temp\`

The scratch directory of `bv`, for machine-generated temporary files: the args files of [hooks](hooks.md#the-hook-args), and the package pins that [`bv dependencies`](tool-commands/dependencies.md) reads from MSBuild.
Add it to `.gitignore`.
`bv` never counts its content as a working-tree change during a release, but without the ignore entry Git tooling shows the files as untracked.
`bv clean` deletes the directory.

---

## `dotnet-tools.json`

[`dotnet-tools.json`](https://learn.microsoft.com/en-us/dotnet/core/tools/local-tools-how-to-use) is the .NET local tool manifest.
It pins the versions of the .NET tools the repository uses, so that `dotnet <tool>` runs the pinned version.
A repository that uses Buildvana usually pins `bv` there.
The manifest is optional: `bv` also runs as a global tool, or through `dnx`.

The manifest sits in the home directory.
`bv` reads that manifest, and never one of a directory above the home directory.
When the manifest pins `bv`, the pinned `bv` runs in place of the invoked one.
[Delegation](command-line.md#delegation) says when `bv` delegates, and what the delegated run gets.
[`bv self-update`](tool-commands/self-update.md) moves the `bv` pin, and creates the manifest when the home directory has none.
A manifest in a subdirectory pins the tools run from there, and [`bv dependencies`](tool-commands/dependencies.md) manages its pins as well.

`bv` does not read `.config\dotnet-tools.json`.
When the home directory, or a directory under it, holds one, `bv` stops with an error that names the file and the move.
[Migration from `.config\dotnet-tools.json`](#migration-from-configdotnet-toolsjson) says why, and how to move the file.

### Migration from `.config\dotnet-tools.json`

The .NET SDK before version 10 created the manifest as `.config\dotnet-tools.json`, and the dotnet CLI reads it in either place.
`bv` reads `dotnet-tools.json` alone, where the .NET SDK creates it.
Move the manifest up one level, and delete `.config\` when it is empty:

```shell
git mv .config/dotnet-tools.json dotnet-tools.json
```

When `dotnet-tools.json` exists as well, the dotnet CLI reads the two files as one manifest.
Merge the tools of `.config\dotnet-tools.json` into `dotnet-tools.json` instead, then delete `.config\dotnet-tools.json`.

Then update every path that names the file: CI workflows, cache keys, and scripts.
A cache key that hashes `.config/dotnet-tools.json` matches nothing after the move, so the key stops changing.
`dotnet tool restore` needs no change, because the dotnet CLI reads the manifest in either place.

---

## `artifacts\`

The directory where the results of a build go: NuGet packages, setup executables, publish directories.
Buildvana SDK creates it when it does not exist.

---

## `src\`, `tests\`, `samples\`

The one rule about the location of a project is that it resides under the home directory.
Three locations are recommended:

- `src\` for the product;
- `tests\` for the test projects;
- `samples\` for the sample projects.

```text
<home_directory>\
|
+--- samples\
|    |
|    +--- Sample1\                <<< Sample project showing the use of MyLibrary
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
|    +--- MyLibrary\              <<< The library, distributed as a NuGet package
|    |    |
|    |    +--- MyLibrary.csproj
|    |    +--- ...
|    |
|    +--- MyLibrary.Extras\       <<< Additional features for MyLibrary, distributed as a separate package
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
|    +--- MyLibrary.Extras.Tests\ <<< Unit tests for MyLibrary.Extras
|    |    |
|    |    +--- MyLibrary.Extras.Tests.csproj
|    |    +--- ...
|    |
|    +--- Common.props
|    +--- Common.targets
|
+--- MyLibrary.slnx
+--- ...
```

Grouping projects this way pays off once the parts they share, such as common dependencies, go in the `Common.props` and `Common.targets` files of the group.
[The next section](#commonprops-and-commontargets) explains those files.

---

## `Common.props` and `Common.targets`

MSBuild [imports](https://learn.microsoft.com/visualstudio/msbuild/customize-your-build#directorybuildprops-and-directorybuildtargets) a `Directory.Build.props` and a `Directory.Build.targets` file into every project under their directory.
They hold the properties and build settings that projects share.
MSBuild imports only the first such file it finds, walking up from the directory of the project.
With one `Directory.Build.props` in the home directory and one in `src\`, MSBuild sees the second alone, unless it imports the first.
The `<Import>` element may sit anywhere in the file, and a reader cannot tell which file's properties win.
A `Directory.Build.props` in a directory above the repository is imported the same way, and changes the build without a trace in the repository.

Buildvana SDK replaces both files with `Common.props` and `Common.targets`.
They serve the same purpose: the properties and build instructions that every project under a directory shares, such as `Owners`, `Company`, and `Copyright`.
Buildvana SDK imports every `Common.props` from the home directory down to the directory of the project, in that order.
A subdirectory therefore overrides what a parent directory sets.
It imports at most ten of them, and never one outside the home directory.

Buildvana SDK also imports `BeforeCommon.props` and `AfterCommon.props`, and the `.targets` counterparts of all three, by the same walk.
Every `BeforeCommon.props` comes before every `Common.props`, and every `AfterCommon.props` after.
An `AfterCommon.props` in the home directory therefore overrides what a `Common.props` in a subdirectory sets.

A `Common.props` in the home directory:

```xml
<Project>

  <!-- Common project and package metadata -->
  <PropertyGroup>
    <Product>MyProduct</Product>
    <Authors>myself</Authors> <!-- My NuGet account -->
    <Owners>mycompany</Owners> <!-- The NuGet account of the company, which uploads the packages -->
    <Company>MyCompany, Inc.</Company>
    <Copyright>Copyright (C) 2018-2026 MyCompany, Inc.</Copyright>
    <PackageReleaseNotes>A changelog is available at $(PackageProjectUrl)/blob/main/CHANGELOG.md</PackageReleaseNotes>
  </PropertyGroup>

</Project>
```

A `tests\Common.props`:

```xml
<Project>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="TUnit" />
  </ItemGroup>

</Project>
```

The package reference states no version, because the repository [manages package versions centrally](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management), which Buildvana recommends.

A `Common.targets` file is needed less often.
It holds, for example, a target that runs [before or after the build](https://learn.microsoft.com/en-us/visualstudio/msbuild/how-to-extend-the-visual-studio-build-process) of every project under its directory.

---

## `Directory.Build.props` and `Directory.Build.targets`

The two files are the exception to the rule of the previous section.
They sit in the home directory and serve two purposes.
They import `Sdk.props` and `Sdk.targets` from the Buildvana SDK package, and they keep MSBuild from importing a `Directory.Build.props` or `Directory.Build.targets` from outside the repository.

`Directory.Build.props` holds:

```xml
<Project>

  <Import Project="Sdk.props" Sdk="Buildvana.Sdk" /> <!-- The Buildvana.Sdk version is specified in global.json -->

</Project>
```

`Directory.Build.targets` holds:

```xml
<Project>

  <Import Project="Sdk.targets" Sdk="Buildvana.Sdk" /> <!-- The Buildvana.Sdk version is specified in global.json -->

</Project>
```

Neither `<Import>` carries a `Version` attribute.
[`global.json`](#globaljson) pins the version of Buildvana SDK once, for the whole repository.
The two files are therefore identical across repositories, and import `Sdk.props` and `Sdk.targets` from one version.
When a stray `Version` attribute makes them come from two versions, Buildvana SDK raises error [BVSDK1002](sdk-diagnostics.md#buildvana-sdk-core-1000-1049).

Prefer `Common.props` and `Common.targets` to a `Directory.Build.props` or `Directory.Build.targets` elsewhere in the repository.
Where one exists, it must import the file of the home directory, as [The build environment](hooks.md#the-build-environment) says for `.buildvana\`.

---

## `global.json`

[`global.json`](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json) is where the .NET SDK reads the version of an MSBuild project SDK referenced without a version, under the `msbuild-sdks` key.
The `<Import>` elements of `Directory.Build.props` and `Directory.Build.targets` reference Buildvana SDK without a `Version` attribute, so the file pins the version of Buildvana SDK:

```json
{
  "msbuild-sdks": {
    "Buildvana.Sdk": "1.0.0"
  }
}
```

The file also pins the version of the .NET SDK, under the `sdk` key, and the two uses coexist.

Before running, a command that uses Buildvana SDK checks that the file pins `Buildvana.Sdk` at the version of the running `bv`.
[The SDK version check](command-line.md#the-sdk-version-check) says which commands check, and how to skip the check.
[`bv self-update`](tool-commands/self-update.md) re-pins the file to the version of the running `bv`.

---

## `VERSION`

A plain-text file in the home directory, holding the `MAJOR.MINOR[-[tag]]` version specification of the product:

```text
2.0-preview
```

The [`Versioning` module](sdk-modules/versioning.md) reads it, and computes the patch number from the Git history.
`bv release` and `bv version advance` rewrite the file when they advance the version.
