# `AlternatePack` module

This module replaces NuGet packing with two pack methods: publishing the project to folders, and building Windows setup programs with [Inno Setup](https://jrsoftware.org/isinfo.php).
`dotnet pack` runs both, and the zip files and setup programs they produce join the release asset list that [`bv release` publishes](../tool-commands/release.md#publishing).

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Configuration](#configuration)
  - [`UseAlternatePack` property](#usealternatepack-property)
  - [`InnoSetupCompiler` property](#innosetupcompiler-property)
  - [Script constants](#script-constants)
- [Usage](#usage)
  - [`Pack` target](#pack-target)
  - [`PublishFolder` items](#publishfolder-items)
  - [`InnoSetup` items](#innosetup-items)
  - [`InnoSetupConstant` items](#innosetupconstant-items)
  - [Include file](#include-file)
  - [Running under Wine](#running-under-wine)
- [Diagnostics](#diagnostics)

---

## Configuration

### `UseAlternatePack` property

Set it to `true` to turn the module on.
The default is `false`.

The module then sets `IncludeNuGetPackSupport` to `false`, which turns the [`NuGetPack` module](nuget-pack.md) off, and skips the import of the NuGet pack targets.
[`Pack` target](#pack-target) says what runs in their place.

### `InnoSetupCompiler` property

The path of `ISCC`, the Inno Setup compiler.
The default is the `ISCC.exe` of the `Tools.InnoSetup` package, which the module references when the project has an [`InnoSetup` item](#innosetup-items).
A `PackageReference` of yours to `Tools.InnoSetup` replaces the module's reference, and under central package management a `PackageVersion` item sets its version.

### Script constants

Every Inno Setup script receives these constants, and a property sets each one.
`APP_EXENAME` has no property of its own.

| Constant                | Property               | Default                                    |
| ----------------------- | ---------------------- | ------------------------------------------ |
| `COMPANY_SHORTNAME`     | `CompanyShortName`     | `$(Company)`                               |
| `COMPANY_FULLNAME`      | `CompanyFullName`      | `$(Company)`                               |
| `COMPANY_WEBSITE`       | `CompanyWebSite`       | empty                                      |
| `COMPANY_SUPPORTPHONE`  | `CompanySupportPhone`  | empty                                      |
| `APP_EXENAME`           | none                   | `$(AssemblyName)`                          |
| `APP_SHORTNAME`         | `AppShortName`         | `$(AssemblyName)`                          |
| `APP_FULLNAME`          | `AppFullName`          | `$(AssemblyTitle)`, else `$(AssemblyName)` |
| `APP_COMMENTS`          | `AppComments`          | empty                                      |
| `APP_CONTACT`           | `AppContact`           | empty                                      |
| `APP_COPYRIGHT`         | `AppCopyright`         | `$(Copyright)`                             |
| `APP_DESCRIPTION`       | `AppDescription`       | `$(Description)`                           |
| `APP_VERSION`           | `AppVersion`           | `$(VersionPrefix)`                         |
| `APP_SEMANTIC_VERSION`  | `AppSemanticVersion`   | `$(AssemblyInformationalVersion)`          |
| `APP_MINWINDOWSVERSION` | `AppMinWindowsVersion` | `10.0.10240`                               |

`AppShortName` is also the base of the default name of a setup program.
The default of `AppMinWindowsVersion` is the first release of Windows 10.

---

## Usage

### `Pack` target

`dotnet pack` of a project with `UseAlternatePack` runs a `Pack` target the module defines, in place of the NuGet one.
The target runs the targets that `PackDependsOn` names, in this order:

1. `GetBuildVersion`, of the [`Versioning` module](versioning.md#getbuildversion-target).
2. The targets that `BeforePack` names, as with NuGet packing.
3. `PublishInPublishFolders`, which publishes the project once per [`PublishFolder` item](#publishfolder-items) and zips the folders whose `CreateZipFile` is `true`.
4. `UseInnoSetup`, which builds one setup program per [`InnoSetup` item](#innosetup-items).

The order lets a setup program take its files from a publish folder.
After `Pack`, the `RemoveTemporaryPublishFolders` target deletes the folders whose `Temporary` is `true`.
A target of yours runs before step 3 or step 4 when `PublishInPublishFoldersDependsOn` or `UseInnoSetupDependsOn` names it.

The module sets `IsPackable` to `true`, so that `dotnet pack` of the solution packs the project.
It declares the `Pack` project capability, as the NuGet pack targets do, and defaults `PackageId` to `$(AssemblyName)` and `PackageVersion` to `$(Version)`.

### `PublishFolder` items

An item publishes the project to one folder.
The identity names the folder, and the metadata says where the folder goes and what becomes of it.
Declare the items outside any target.

```xml
<PropertyGroup>
  <UseAlternatePack>true</UseAlternatePack>
</PropertyGroup>

<ItemGroup>
  <PublishFolder Include="win-x64" RuntimeIdentifier="win-x64" CreateZipFile="true" />
  <PublishFolder Include="linux-x64" RuntimeIdentifier="linux-x64" CreateZipFile="true" />
</ItemGroup>
```

For version 1.2.3 of a project named `MyApp`, in the `Release` configuration, the example publishes to `artifacts/Release/MyApp/win-x64/` and `artifacts/Release/MyApp/linux-x64/`.
The zip files are `artifacts/Release/MyApp-win-x64_1.2.3.zip` and `artifacts/Release/MyApp-linux-x64_1.2.3.zip`.

| Metadata                  | Default                                                                                               | Meaning                                                                                            |
| ------------------------- | ----------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| `PublishDir`              | `$(ArtifactsDirectory)$(Configuration)/<project>/<identity>/`                                         | The folder the project is published to.                                                            |
| `TargetFramework`         | none                                                                                                  | The target framework to publish.                                                                   |
| `RuntimeIdentifier`       | none                                                                                                  | The runtime identifier to publish for.                                                             |
| `Properties`              | none                                                                                                  | Further global properties for the `Publish` target, as `Name=Value` pairs separated by semicolons. |
| `Temporary`               | `false`                                                                                               | `true` deletes the folder after `Pack`.                                                            |
| `CreateZipFile`           | `false`, or `true` when `ZipFileName` is set                                                          | Whether the folder is zipped.                                                                      |
| `UniqueZipFileName`       | `false` when one folder is zipped, `true` when more are                                               | Whether the default `ZipFileName` includes the identity.                                           |
| `ZipFileName`             | `<project>_<version>.zip`, or `<project>-<identity>_<version>.zip` when `UniqueZipFileName` is `true` | The name of the zip file.                                                                          |
| `IsReleaseAsset`          | `true` when zipped, `false` otherwise                                                                 | Whether the zip file joins the release asset list.                                                 |
| `ReleaseAssetMimeType`    | `application/zip`                                                                                     | The MIME type of the zip file in the list.                                                         |
| `ReleaseAssetDescription` | none                                                                                                  | The description of the zip file in the list.                                                       |

`<project>` is `$(MSBuildProjectName)`, `<version>` is `$(AssemblyInformationalVersion)`, and `$(ArtifactsDirectory)` is the [`artifacts\`](../directory-structure.md#artifacts) directory of the home directory.
When `AssemblyInformationalVersion` is empty, the `_<version>` part is absent.
A folder counts as zipped when its `CreateZipFile` is `true` or its `ZipFileName` is set.

For each item, the module runs the `Publish` target of the .NET SDK with `PublishProtocol` set to `FileSystem` and `PublishDir` set to the folder.
`TargetFramework`, `RuntimeIdentifier`, the pairs of `Properties`, and `PublishingFolder`, set to the identity, reach the target as global properties.
A condition on `PublishingFolder` lets a project file vary its content per folder.
A project with several target frameworks states `TargetFramework` on every item, because the .NET SDK refuses to publish such a project without one.

The zip file goes in `$(ArtifactsDirectory)$(Configuration)/`, and joins the release asset list when `GenerateReleaseAssetList` is `true`.
`Temporary` is for a folder that exists only to be zipped or to feed a setup program.

### `InnoSetup` items

An item builds one setup program from one Inno Setup script.
Declare the items outside any target, because the reference to `Tools.InnoSetup` and the `NeedWine` item depend on them at evaluation.

```xml
<ItemGroup>
  <PublishFolder Include="win-x64" RuntimeIdentifier="win-x64" Temporary="true" />
  <InnoSetup Include="win-x64" Script="Setup/MyApp.iss" SourcePublishFolder="win-x64" />
</ItemGroup>
```

The example publishes the project, builds `artifacts/Release/MyApp_1.2.3.exe` with the publish folder as the `SourceDir` of the script, and deletes the folder.

| Metadata                  | Default                                                                                                  | Meaning                                                                   |
| ------------------------- | -------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| `Script`                  | required                                                                                                 | The path of the script, relative to the project directory.                |
| `SourcePublishFolder`     | none                                                                                                     | The identity of the `PublishFolder` item whose folder is the `SourceDir`. |
| `SourceDir`               | the folder of `SourcePublishFolder`                                                                      | The directory the source paths of the script are relative to.             |
| `TargetFramework`         | none                                                                                                     | Reaches the script as `SETUP_TARGETFRAMEWORK`.                            |
| `RuntimeIdentifier`       | none                                                                                                     | Reaches the script as `SETUP_RUNTIMEIDENTIFIER`.                          |
| `OutputDir`               | `$(ArtifactsDirectory)$(Configuration)/`                                                                 | The directory of the setup program, relative to the default.              |
| `UniqueOutputName`        | `false` with one item, `true` with more                                                                  | Whether the default `OutputName` includes the identity.                   |
| `OutputName`              | `$(AppShortName)_<version>`, or `$(AppShortName)-<identity>_<version>` when `UniqueOutputName` is `true` | The file name of the setup program, without `.exe`.                       |
| `IsReleaseAsset`          | `true`                                                                                                   | Whether the setup program joins the release asset list.                   |
| `ReleaseAssetDescription` | none                                                                                                     | The description of the setup program in the list.                         |

`<version>` is `$(AssemblyInformationalVersion)`.
The setup program joins the release asset list with the MIME type `application/octet-stream`, when `GenerateReleaseAssetList` is `true`.

The module raises an error when:

- an item has no `Script`, BVSDK1900;
- the `Script` names no file, BVSDK1901;
- the `SourcePublishFolder` names no item, BVSDK1902.

### `InnoSetupConstant` items

An item defines a constant for every script, with the identity as its name and the `Value` metadata as its value.
Set `IsPath` to `true` on a constant that holds a path, so that [running under Wine](#running-under-wine) converts it.

```xml
<ItemGroup>
  <InnoSetupConstant Include="LICENSE_FILE" Value="$(MSBuildProjectDirectory)/LICENSE.txt" IsPath="true" />
</ItemGroup>
```

The constants of the [Script constants](#script-constants) table and the per-item constants of the [include file](#include-file) win over an item of the same name.

### Include file

For each `InnoSetup` item, the module writes an include file, `<identity>.iss`, under the `obj/<configuration>/` directory of the project.
`ISCC` receives it through the `/J` option, which [emulates an `#include`](https://jrsoftware.org/ishelp/topic_isppcc.htm) of the file at the top of the script.

The file holds:

- one `#define NAME 'value'` line per constant, with a `'` in the value doubled;
- the per-item constants of the table below;
- a `[Setup]` section with the `SourceDir` directive when `SourceDir` is set, and the `OutputDir` and `OutputBaseFilename` directives from `OutputDir` and `OutputName`.

| Constant                  | Value                            |
| ------------------------- | -------------------------------- |
| `SETUP_IDENTITY`          | The item identity.               |
| `SETUP_PUBLISHFOLDER`     | `SourcePublishFolder`, when set. |
| `SETUP_TARGETFRAMEWORK`   | `TargetFramework`, when set.     |
| `SETUP_RUNTIMEIDENTIFIER` | `RuntimeIdentifier`, when set.   |
| `SETUP_SOURCEDIR`         | `SourceDir`, when set.           |
| `SETUP_OUTPUTDIR`         | `OutputDir`.                     |
| `SETUP_OUTPUTNAME`        | `OutputName`.                    |

### Running under Wine

Inno Setup is a Windows program.
When the project has an `InnoSetup` item, the module declares a [`NeedWine` item](wine.md#needwine-items).
On Linux and macOS, a build with no `WineCommand` then fails with BVSDK2200.

When [`UseWine`](wine.md#usewine-property) is `true`, the module runs `ISCC` through `WineCommand`.
It first [converts to Windows paths](wine.md#converting-paths):

- the paths of the compiler, the script, and the include file;
- the constants whose `IsPath` is `true`;
- the `SourceDir` and `OutputDir` directives.

---

## Diagnostics

The module raises the diagnostics of the [AlternatePack module (1900-1999)](../sdk-diagnostics.md#alternatepack-module-1900-1999) range.
