# Diagnostics issued by Buildvana SDK

Every diagnostic Buildvana SDK raises has a `BVSDK` prefix and a four-digit number from 1000 up.
This page lists them by range, with the severity, the message, and the meaning of each.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Ranges](#ranges)
- [Buildvana SDK core (1000-1049)](#buildvana-sdk-core-1000-1049)
- [Buildvana SDK tasks (1050-1099)](#buildvana-sdk-tasks-1050-1099)
- [Source generators (1100-1199)](#source-generators-1100-1199)
- [AssemblySigning module (1200-1299)](#assemblysigning-module-1200-1299)
- [JetBrainsAnnotations module (1300-1399)](#jetbrainsannotations-module-1300-1399)
- [AdditionalAssemblyInfo module (1400-1499)](#additionalassemblyinfo-module-1400-1499)
- [NuGetPack module (1500-1599)](#nugetpack-module-1500-1599)
- [StandardAnalyzers module (1700-1799)](#standardanalyzers-module-1700-1799)
- [XmlDocumentation module (1800-1899)](#xmldocumentation-module-1800-1899)
- [AlternatePack module (1900-1999)](#alternatepack-module-1900-1999)
- [Versioning module (2000-2099)](#versioning-module-2000-2099)
- [ReleaseAssetList module (2100-2199)](#releaseassetlist-module-2100-2199)
- [Wine module (2200-2299)](#wine-module-2200-2299)
- [ThisAssemblyClass module (2300-2399)](#thisassemblyclass-module-2300-2399)

---

## Ranges

Each module owns a range of 100 numbers, as the sections below list them.
The first two ranges belong to Buildvana SDK itself, and the third to its source generators.
A `...` in a message stands for a value the message carries, such as a file name.

---

## Buildvana SDK core (1000-1049)

| Code      | Severity | Message                                                          | Description                                                                                                                                  |
| --------- | :------: | ---------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| BVSDK1000 |  Error   | Sdk.props not imported.                                          | `Sdk.targets` was imported, and `Sdk.props` was not.                                                                                         |
| BVSDK1001 |  Error   | Sdk.targets not imported.                                        | `Sdk.props` was imported, and `Sdk.targets` was not.                                                                                         |
| BVSDK1002 |  Error   | Sdk.props and Sdk.targets are in different directories.          | `Sdk.props` and `Sdk.targets` come from two versions of Buildvana SDK. Look for a stray `Version` attribute on the `<Import>` elements.      |
| BVSDK1003 |  Error   | Home directory not defined.                                      | No [home marker](directory-structure.md#location-of-the-home-directory) exists above the project directory, so `HomeDirectory` has no value. |
| BVSDK1004 |  Error   | Buildvana SDK requires at least MSBuild v...                     | The MSBuild version is below the [minimum](introduction.md#toolchain).                                                                       |
| BVSDK1005 |  Error   | Multiple Buildvana configuration files found: ... Keep only one. | The home directory holds both `buildvana.json` and `buildvana.jsonc`.                                                                        |
| BVSDK1006 |  Error   | BeforeNETSdk.targets not imported.                               | The project SDK does not layer on `Microsoft.NET.Sdk`, or the project overwrote `BeforeMicrosoftNETSdkTargets` instead of appending to it.   |

---

## Buildvana SDK tasks (1050-1099)

The diagnostics below can come from any task in `Buildvana.Sdk.Tasks.dll`.
A diagnostic specific to one task is listed under its module.

| Code      | Severity | Message                                  | Description                                                                                                                                                          |
| --------- | :------: | ---------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| BVSDK1050 |  Error   | Parameter '...' is missing or empty.     | A Buildvana SDK module called a task without a required parameter. [Open an issue](https://github.com/Tenacom/Buildvana/issues/new/choose).                          |
| BVSDK1051 |  Error   | The file '...' could not be created. ... | A task could not write a file. Clean the project and rebuild it. When the problem persists, [open an issue](https://github.com/Tenacom/Buildvana/issues/new/choose). |
| BVSDK1052 |  Error   | The file '...' could not be read. ...    | A task could not read a file. Clean the project and rebuild it. When the problem persists, [open an issue](https://github.com/Tenacom/Buildvana/issues/new/choose).  |

---

## Source generators (1100-1199)

| Code      | Severity | Message                                                                   | Description                                                                                                                                              |
| --------- | :------: | ------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| BVSDK1100 |  Error   | Buildvana SDK source generators are only supported in C# projects.        | The language of the project is not C#.                                                                                                                   |
| BVSDK1101 |  Error   | Buildvana SDK source generators require Roslyn version ... or later (...) | The Roslyn version is below the minimum. [Toolchain](introduction.md#toolchain) lists the .NET SDK and Visual Studio versions that ship a supported one. |

---

## AssemblySigning module (1200-1299)

| Code      | Severity | Message                                               | Description                                                                                                                                     |
| --------- | :------: | ----------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| BVSDK1200 |  Error   | Certificate file '...' not found.                     | The `.pfx` file that `AssemblyOriginatorKeyFile` names does not exist.                                                                          |
| BVSDK1201 |  Error   | Cannot extract certificate from '...'.                | The `.pfx` file that `AssemblyOriginatorKeyFile` names is invalid, or `AssemblyOriginatorKeyPassword` holds the wrong password, or no password. |
| BVSDK1202 |  Error   | '...' does not contain an exportable RSA private key. | The `.pfx` file that `AssemblyOriginatorKeyFile` names holds no RSA private key that can be exported to a `.snk` file.                          |

---

## JetBrainsAnnotations module (1300-1399)

| Code      | Severity | Message                                               | Description                                                                                                                                                                                               |
| --------- | :------: | ----------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| BVSDK1300 |  Error   | Could not export JetBrains annotations for '...'. ... | The export of the ReSharper external annotations failed, and the message says why. Clean and rebuild. When the problem persists, [open an issue](https://github.com/Tenacom/Buildvana/issues/new/choose). |

---

## AdditionalAssemblyInfo module (1400-1499)

| Code      | Severity | Message                                                                  | Description                                                                                                            |
| --------- | :------: | ------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------- |
| BVSDK1400 | Warning  | Additional assembly info generation is not supported for language '...'. | `GenerateAdditionalAssemblyInfo` is `true` in a project whose language is not C#, and Buildvana SDK generates nothing. |

---

## NuGetPack module (1500-1599)

| Code      | Severity | Message                                                 | Description                                                                                                                                         |
| --------- | :------: | ------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| BVSDK1500 |  Error   | Specified license file '...' does not exist.            | The file that `PackageLicensePath` names does not exist.                                                                                            |
| BVSDK1501 |  Error   | Specified license file '...' was not found.             | The file that `PackageLicenseFile` names exists neither in the project directory nor in a directory above it.                                       |
| BVSDK1502 |  Error   | No license file found.                                  | No license file was specified, and no default license file exists. Set `LicenseFileInPackage` to `false` to pack no license file.                   |
| BVSDK1503 |  Error   | Specified third-party notice file '...' does not exist. | The file that `PackageThirdPartyNoticePath` names does not exist.                                                                                   |
| BVSDK1504 |  Error   | Specified third-party notice file '...' was not found.  | The file that `PackageThirdPartyNoticeFile` names exists neither in the project directory nor in a directory above it.                              |
| BVSDK1505 |  Error   | No third-party notice file found.                       | No third-party notice file was specified, and no default one exists. Set `ThirdPartyNoticeInPackage` to `false` to pack no third-party notice file. |
| BVSDK1506 |  Error   | Specified package icon file '...' does not exist.       | The file that `PackageIconPath` names does not exist.                                                                                               |
| BVSDK1507 |  Error   | Specified package icon file '...' was not found.        | The file that `PackageIcon` names exists neither in the project directory nor in a directory above it.                                              |
| BVSDK1508 |  Error   | No package icon file found.                             | No package icon file was specified, and no default one exists. Set `IconInPackage` to `false` to pack no icon.                                      |
| BVSDK1509 |  Error   | Specified README file '...' does not exist.             | The file that `PackageReadmePath` names does not exist.                                                                                             |
| BVSDK1510 |  Error   | Specified README file '...' was not found.              | The file that `PackageReadmeFile` names exists neither in the project directory nor in a directory above it.                                        |
| BVSDK1511 |  Error   | No README file found for package.                       | No README file was specified, and no default one exists. Set `ReadmeFileInPackage` to `false` to pack no README file.                               |

---

## StandardAnalyzers module (1700-1799)

The module raises no diagnostic.

---

## XmlDocumentation module (1800-1899)

The module raises no diagnostic.

---

## AlternatePack module (1900-1999)

| Code      | Severity | Message                                                          | Description                                                                              |
| --------- | :------: | ---------------------------------------------------------------- | ---------------------------------------------------------------------------------------- |
| BVSDK1900 |  Error   | InnoSetup item '...' does not specify a script.                  | An `InnoSetup` item has no `Script` metadata.                                            |
| BVSDK1901 |  Error   | InnoSetup script '...' referenced by '...' does not exist.       | The `Script` metadata of an `InnoSetup` item names a file that does not exist.           |
| BVSDK1902 |  Error   | InnoSetup item '...' refers to non-existent PublishFolder '...'. | The `SourcePublishFolder` metadata of an `InnoSetup` item names no `PublishFolder` item. |

---

## Versioning module (2000-2099)

| Code      | Severity | Message                                             | Description                                                                |
| --------- | :------: | --------------------------------------------------- | -------------------------------------------------------------------------- |
| BVSDK2000 |  Error   | Version file (VERSION) not found in home directory. | `UseVersioning` is `true`, and the home directory holds no `VERSION` file. |

---

## ReleaseAssetList module (2100-2199)

The module raises no diagnostic.

---

## Wine module (2200-2299)

| Code      | Severity | Message                                                                                                 | Description                                                                                                                                                                    |
| --------- | :------: | ------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| BVSDK2200 |  Error   | One or more tools need Wine to run on this system, but no Wine invocation command has been defined: ... | A `NeedWine` item exists, the build runs on Linux or macOS, and `WineCommand` is empty. [`WineCommand` property](sdk-modules/wine.md#winecommand-property) says how to set it. |

---

## ThisAssemblyClass module (2300-2399)

| Code      | Severity | Message                                                                          | Description                                                                                                                                                                      |
| --------- | :------: | -------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| BVSDK2300 | Warning  | ThisAssembly class generation is only supported in C# projects (Language='...'). | `GenerateThisAssemblyClass` is `true` in a project whose language is not C#, and Buildvana SDK generates no class.                                                               |
| BVSDK2301 |  Error   | Constant '...' has invalid value '...'                                           | The `Value` metadata of a `ThisAssemblyConstant` item does not follow the [constants syntax](constants-syntax.md): the type is unknown, or the value does not parse as the type. |
