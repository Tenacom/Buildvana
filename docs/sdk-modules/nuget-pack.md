# `NuGetPack` module

This module packs a README, a license, a third-party notice, and an icon into the NuGet package of every packable project.
When the project names none, the module finds each file by name, in the project directory and above.
It also sets the package output directory, the defaults of the package metadata, and the properties a `.nuspec` file reads.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Configuration](#configuration)
  - [`IncludeNuGetPackSupport` property](#includenugetpacksupport-property)
  - [`IsPackable` property](#ispackable-property)
  - [README](#readme)
  - [License](#license)
  - [Third-party notice](#third-party-notice)
  - [Icon](#icon)
  - [Package metadata](#package-metadata)
- [Usage](#usage)
  - [Packed files](#packed-files)
  - [Output directory](#output-directory)
  - [`.nuspec` file](#nuspec-file)
- [Diagnostics](#diagnostics)

---

## Configuration

### `IncludeNuGetPackSupport` property

Set it to `false` to turn the module off.
The default is `true`.
The [`AlternatePack` module](alternate-pack.md#usealternatepack-property) sets it to `false` when `UseAlternatePack` is `true`.

With the module off, `dotnet pack` reads the NuGet properties alone, and `IsPackable` defaults to `true` for every project.

### `IsPackable` property

`dotnet pack` packs the project when it is `true`, and skips it otherwise.
The default is `true` for a [library project](../internal-use-properties.md#project-type) and `false` for every other project, where NuGet defaults it to `true` for every project.
Any value other than `true` counts as `false`.

A packable project needs a README, a license, a third-party notice, and an icon, as the four sections below say.
Set it to `false` on a library that is not published.
Set it to `true` on a console project published as a .NET tool.
An application packed by the [`AlternatePack` module](alternate-pack.md#pack-target) is packable too, and that module sets the property to `true` itself.

### README

`ReadmeFileInPackage` set to `false` packs no README.
The default is `true`.

`PackageReadmeFile` names the file, and the module looks it up in the project directory, then in each directory above it.
`PackageReadmePath` is the full path of the file, and wins over `PackageReadmeFile`.
State one of the two, and the module fills in the other from the file.

With neither set, the module takes the first of these names it finds: `Package-README.md`, `package-readme.md`, `NuGet-README.md`, `nuget-readme.md`, `NuGet.md`, `nuget.md`, `README.md`, `readme.md`.
NuGet accepts a Markdown README alone.

The build fails with BVSDK1509 when `PackageReadmePath` names no file, BVSDK1510 when `PackageReadmeFile` is found nowhere, and BVSDK1511 when no default exists.

### License

`PackageLicenseExpression` states the license as an [SPDX expression](https://spdx.org/licenses/), such as `MIT`.
With an expression, the module forces `LicenseFileInPackage` to `false`, because NuGet accepts an expression or a file, not both.

`LicenseFileInPackage` set to `false` packs no license file.
The default is `true`.

`PackageLicenseFile` names the file, and the module looks it up in the project directory, then in each directory above it.
`PackageLicensePath` is the full path of the file, and wins over `PackageLicenseFile`.
State one of the two, and the module fills in the other from the file.

With neither set, the module takes the first of these names it finds: `LICENSE`, `LICENSE.txt`, `LICENSE.md`, `License`, `License.txt`, `License.md`, `license`, `license.txt`, `license.md`.

The build fails with BVSDK1500 when `PackageLicensePath` names no file, BVSDK1501 when `PackageLicenseFile` is found nowhere, and BVSDK1502 when no default exists.

### Third-party notice

A third-party notice lists the copyright notices and licenses of the third-party code the package contains.
NuGet has no property for it, and the module packs the file at the root of the package, next to the license.

`ThirdPartyNoticeInPackage` set to `false` packs no third-party notice.
The default is `true`.

`PackageThirdPartyNoticeFile` names the file, and the module looks it up in the project directory, then in each directory above it.
`PackageThirdPartyNoticePath` is the full path of the file, and wins over `PackageThirdPartyNoticeFile`.
State one of the two, and the module fills in the other from the file.

With neither set, the module takes the first of these names it finds: `THIRD-PARTY-NOTICES`, `THIRD-PARTY-NOTICES.txt`, `THIRD-PARTY-NOTICES.md`, `third-party-notices`, `third-party-notices.txt`, `third-party-notices.md`, `ThirdPartyNotices`, `ThirdPartyNotices.txt`, `ThirdPartyNotices.md`.

The build fails with BVSDK1503 when `PackageThirdPartyNoticePath` names no file, BVSDK1504 when `PackageThirdPartyNoticeFile` is found nowhere, and BVSDK1505 when no default exists.

### Icon

`IconInPackage` set to `false` packs no icon.
The default is `true`.

`PackageIcon` names the file, and the module looks it up in the project directory, then in each directory above it.
`PackageIconPath` is the full path of the file, and wins over `PackageIcon`.
State one of the two, and the module fills in the other from the file.

With neither set, the module looks up four names in this order: `<project>.package.png`, `<product>.package.png`, `PackageIcon.png`, and `icon.png`.
For each name, it searches the project directory and above, then a `graphics` subdirectory of each of those directories, then a `branding` subdirectory.
The first file found wins.
`<project>` is `$(MSBuildProjectName)`, and `<product>` is `$(Product)`.
NuGet accepts a PNG or JPEG file of at most 1 MB, and recommends 128 by 128 pixels.

The build fails with BVSDK1506 when `PackageIconPath` names no file, BVSDK1507 when `PackageIcon` is found nowhere, and BVSDK1508 when no default exists.

### Package metadata

The module sets the defaults of these properties, which the [`pack` target](https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets#pack-target) of NuGet and a [`.nuspec` file](#nuspec-file) read.

| Property                          | Default            | Meaning                                                                                                                        |
| --------------------------------- | ------------------ | ------------------------------------------------------------------------------------------------------------------------------ |
| `Owners`                          | `$(Authors)`       | The owners of the package.                                                                                                     |
| `PackageRequireLicenseAcceptance` | `false`            | Whether a consumer must accept the license before installing the package.                                                      |
| `DevelopmentDependency`           | `false`            | Whether the package is a development-only dependency, which never appears in the dependencies of a package that references it. |
| `PackageType`                     | `Dependency`       | The intended use of the package.                                                                                               |
| `Serviceable`                     | `true`             | A flag NuGet reserves for its own use.                                                                                         |
| `PackageTitle`                    | `$(AssemblyTitle)` | The title of the package.                                                                                                      |
| `SourceRevisionId`                | `0`                | The source control revision the package was built from.                                                                        |

Any `PackageRequireLicenseAcceptance` value other than `true` counts as `false`.
The module forces it to `false` when there is neither a license expression nor a license file, because there is no license to accept.
`Owners`, `PackageTitle`, and `SourceRevisionId` reach the package through a `.nuspec` file alone, because the `pack` target reads none of them.

---

## Usage

### Packed files

Each of the four files joins the project as a `None` item with `Pack` set to `true`.
`dotnet pack` puts the file at the root of the package.
A `None` item of yours for the same file gives way to the item of the module.
Visual Studio shows the items under a `- Package` folder of the project, and the build copies none of them to the output directory.

The lookups run when MSBuild evaluates the project, so a missing file fails `dotnet build` as well as `dotnet pack`.
In the default lookup, a file above the [home directory](../directory-structure.md#home-directory) does not count, and the lookup goes on with the next name.
The [directory structure](../directory-structure.md#layout) page puts `LICENSE`, `README.md`, and `THIRD-PARTY-NOTICES` in the home directory, where every project of the repository finds them.

### Output directory

`dotnet pack` writes the package to `artifacts/<Configuration>/` in the home directory, because the module sets `PackageOutputPath` to `$(ArtifactsDirectory)$(Configuration)/`.
`$(ArtifactsDirectory)` is the [`artifacts\`](../directory-structure.md#artifacts) directory of the home directory.
The module overwrites a `PackageOutputPath` set in a project file, and a property passed on the command line alone wins.

### `.nuspec` file

When a `<project>.nuspec` file sits beside the project file, the module sets `NuspecFile` to it.
`dotnet pack` then [packs from the file](https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets#packing-using-a-nuspec-file) instead of the project properties.
`NuspecBasePath` defaults to the project directory.
Visual Studio shows the file under the `- Package` folder, and the file itself is not packed.

Before `GenerateNuspec` runs, the `BV_SetNuspecProperties` target fills `NuspecProperties` with these tokens, which the file reads as `$token$`:

| Token                      | Property                          |
| -------------------------- | --------------------------------- |
| `minClientVersion`         | `MinClientVersion`                |
| `id`                       | `PackageId`                       |
| `title`                    | `PackageTitle`                    |
| `version`                  | `PackageVersion`                  |
| `summary`                  | `Summary`                         |
| `description`              | `Description`                     |
| `owners`                   | `Owners`                          |
| `authors`                  | `Authors`                         |
| `copyright`                | `Copyright`                       |
| `configuration`            | `Configuration`                   |
| `tags`                     | `PackageTags`                     |
| `repositoryType`           | `RepositoryType`                  |
| `repositoryUrl`            | `RepositoryUrl`                   |
| `projectUrl`               | `PackageProjectUrl`               |
| `licenseExpression`        | `PackageLicenseExpression`        |
| `licenseFile`              | `PackageLicenseFile`              |
| `licensePath`              | `PackageLicensePath`              |
| `thirdPartyNoticeFile`     | `PackageThirdPartyNoticeFile`     |
| `thirdPartyNoticePath`     | `PackageThirdPartyNoticePath`     |
| `readmeFile`               | `PackageReadmeFile`               |
| `readmePath`               | `PackageReadmePath`               |
| `icon`                     | `PackageIcon`                     |
| `iconPath`                 | `PackageIconPath`                 |
| `requireLicenseAcceptance` | `PackageRequireLicenseAcceptance` |
| `packageType`              | `PackageType`                     |
| `releaseNotes`             | `PackageReleaseNotes`             |
| `sourceRevisionId`         | `SourceRevisionId`                |
| `developmentDependency`    | `DevelopmentDependency`           |
| `serviceable`              | `Serviceable`                     |

`owners`, `authors`, and `tags` hold the property with every `;` replaced by `,`.
The target overwrites a `NuspecProperties` value of yours.

---

## Diagnostics

The module raises the diagnostics of the [NuGetPack module (1500-1599)](../sdk-diagnostics.md#nugetpack-module-1500-1599) range.
