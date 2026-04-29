# Directory structure

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Overview](#overview)
- [Home directory](#home-directory)
  - [Location of the home directory](#location-of-the-home-directory)
- [`artifacts\`](#artifacts)
- [`src\`, `tests\`, `samples\`](#src-tests-samples)
- [`Common.props` and `Common.targets`](#commonprops-and-commontargets)
- [`Directory.Build.props` and `Directory.Build.targets`](#directorybuildprops-and-directorybuildtargets)
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
+--- Common.props              <<< Common parts of MSBuild projects
+--- Common.targets
|
+--- Directory.Build.props     <<< (*) Scaffold files used to import Buildvana SDK
+--- Directory.Build.targets
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

This is also the root of your repository: it is where you checked out to, or checked in from. In fact, **Buildvana SDK requires that you use a Git repository**, although this requirement may be relaxed in a future version.

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

Buildvana SDK tries to determine the location of the home directory by following the rules below, in the listed order. The first directory found by a rule is assumed to be the home directory and its full path becomes the value of `HomeDirectory`.

- **Git submodule:** starting from the project's directory and going up the directory hierarchy, find a directory that contains a file named `.git`.

- **Git repository:** starting from the project's directory and going up the directory hierarchy, find a directory named `.git` that contains a file named `HEAD`.

If no rule succeeds in identifying a home directory, the build (or project loading in Visual Studio) stops with error [BVE1002](ErrorsAndWarnings.md#buildvana-sdk-1000-1099).

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

These two files are the only exception to the "no-Directory.Build-files" rule outlined [in the previous section](commonprops-and-commontargets).

These files, which must be in the home directory, serve two purposes:

- importing `Sdk.props` and `Sdk.targets`, respectively, from Buildvana SDK's NuGet package, and
- making sure that no other `Directory.Build.*` file from outside the repository is imported.

Here's what must be in `Directory.Build.props`:

```XML
<Project>

  <PropertyGroup>
    <BuildvanaSdkVersion>0.1.0</BuildvanaSdkVersion>
  </PropertyGroup>
  
  <Import Project="Sdk.props" Sdk="Buildvana.Sdk" Version="$(BuildvanaSdkVersion)" />

</Project>
```

As you may have guessed, `Directory.Build.targets` is similar:

```XML
<Project>

  <Import Project="Sdk.targets" Sdk="Buildvana.Sdk" Version="$(BuildvanaSdkVersion)" />

</Project>
```

The `BuildvanaSdkVersion` property makes sure that `Sdk.props` and `SdkTargets` are imported from the same version of Buildvana SDK; otherwise they might be incompatible with each other. Buildvana SDK will detect such a situation and issue a [`BVE1002`](ErrorsAndWarnings.md#buildvana-sdk-1000-1099) error.

It is important that no other `Directory.Build.props` and / or `Directory.Build.targets` files exist in the repository; use `Common.props` and `Common.targets`, instead, as explained above.

## `LICENSE`

TODO

## `README.md`

TODO

## `THIRD-PARTY-NOTICES`

TODO

## `VERSION`

TODO
