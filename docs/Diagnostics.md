# Diagnostics issued by Buildvana SDK

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Overview](#overview)
- [Buildvana SDK core (1000-1049)](#buildvana-sdk-core-1000-1049)
- [Buildvana SDK tasks (1050-1099)](#buildvana-sdk-tasks-1050-1099)
- [Source generators (1100-1199)](#source-generators-1100-1199)
- [AssemblySigning module (1200-1299)](#assemblysigning-module-1200-1299)
- [JetBrainsAnnotations module (1300-1399)](#jetbrainsannotations-module-1300-1399)
- [AdditionalAssemblyInfo module (1400-1499)](#additionalassemblyinfo-module-1400-1499)
- [NuGetPack module (1500-1599)](#nugetpack-module-1500-1599)
- [ReferenceAssemblies module (1600-1699)](#referenceassemblies-module-1600-1699)
- [StandardAnalyzers module (1700-1799)](#standardanalyzers-module-1700-1799)
- [XmlDocumentation module (1800-1899)](#xmldocumentation-module-1800-1899)
- [AlternatePack module (1900-1999)](#alternatepack-module-1900-1999)
- [NerdbankGitVersioning module (2000-2099)](#nerdbankgitversioning-module-2000-2099)
- [ReleaseAssetList module (2100-2199)](#releaseassetlist-module-2100-2199)
- [Wine module (2200-2299)](#wine-module-2200-2299)

## Overview

All diagnostics issued by Buildvana SDK have a `BVSDK` prefix. All numbers start from 1000, so there are no leading zeros.

Each module is assigned a contiguous range of 100 diagnostics, as listed below. The first ranges are reserved for the SDK itself and for source generators.

## Buildvana SDK core (1000-1049)

| Code      | Severity | Message                                                 | Description                                                                                                                                                    |
| --------- | :------: | ------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| BVSDK1000 |  Error   | Sdk.props not imported.                                 | `Sdk.targets` was imported, but `Sdk.props` was not.                                                                                                           |
| BVSDK1001 |  Error   | Sdk.targets not imported.                               | `Sdk.props` was imported, but `Sdk.targets` was not.                                                                                                           |
| BVSDK1002 |  Error   | Sdk.props and Sdk.targets are in different directories. | The `Version` attributes in the `<Import>` directives for `Sdk.props` and `Sdk.targets` do not match.                                                          |
| BVESDK003 |  Error   | Home directory not defined.                             | No suitable value for the `HomeDirectory` property has been found.                                                                                             |
| BVSDK1004 |  Error   | Buildvana SDK requires at least MSBuild v...            | You are trying to use Buildvana SDK with an unsupported version of MSBuild. See [the README](../README.md#toolchain) for a list of supported MSBuild versions. |

## Buildvana SDK tasks (1050-1099)

The following are generic diagnostics that might be issued by any task in `Buildvana.Sdk.Tasks.dll`.  
More task-specific diagnostics (if any) are listed under relevant modules.

| Code      | Severity | Message                              | Description                                                                                                                                                                                            |
| --------- | :------: | ------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| BVSDK1050 |  Error   | Parameter '...' is missing or empty. | Something went wrong with a Buildvana SDK module. Please [open an issue](https://github.com/Tenacom/Buildvana/issues/new/choose).                                                                      |
| BVSDK1051 |  Error   | The file '...' could not be created. | There was an error trying to write to a file. Try cleaning the project and rebuilding it. If the problem arises again, please [open an issue](https://github.com/Tenacom/Buildvana/issues/new/choose). |

## Source generators (1100-1199)

This module has no associated diagnostics.

## AssemblySigning module (1200-1299)

| Code      | Severity | Message                                               | Description                                                                                                                                                                                     |
| --------- | :------: | ----------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| BVSDK1200 |  Error   | Certificate file '...' not found.                     | The `.pfx` certificate file specified by the `AssemblyOriginatorKeyFile` property is missing.                                                                                                   |
| BVSDK1201 |  Error   | Cannot extract certificate from '...'.                | The `.pfx` certificate file specified by the `AssemblyOriginatorKeyFile` property is invalid, or the wrong password (or no password) was given in the `AssemblyOriginatorKeyPassword` property. |
| BVSDK1202 |  Error   | '...' does not contain an exportable RSA private key. | The `.pfx` certificate file specified by the `AssemblyOriginatorKeyFile` property does not contain an RSA private key that can be exported to a `.snk` file.                                    |

## JetBrainsAnnotations module (1300-1399)

This module has no associated diagnostics.

## AdditionalAssemblyInfo module (1400-1499)

| Code      | Severity | Message                                                                  | Description                                                                                                                                                             |
| --------- | :------: | ------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| BVSDK1400 | Warning  | Additional assembly info generation is not supported for language '...'. | Property `GenerateAdditionalAssemblyInfo` was set to `true` in a project whose language is neither C# nor Visual Basic. Additional assembly info will not be generated. |

## NuGetPack module (1500-1599)

| Code      | Severity | Message                                                 | Description                                                                                                                                                                                                                               |
| --------- | :------: | ------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| BVSDK1500 |  Error   | Specified license file '...' does not exist.            | A license file specified by the `PackageLicensePath` property does not exist.                                                                                                                                                             |
| BVSDK1501 |  Error   | Specified license file '...' was not found.             | A license file specified by the `PackageLicenseFile` property was not found in the project's directory nor in any containing directory.                                                                                                   |
| BVSDK1502 |  Error   | No license file found.                                  | No license file was specified, and no default license file has been found. Set the `LicenseFileInPackage` property to `false` to explicitly exclude any license file to be included in the package.                                       |
| BVSDK1503 |  Error   | Specified third-party notice file '...' does not exist. | A third-party notice file specified by the `PackageThirdPartyNoticePath` property does not exist.                                                                                                                                         |
| BVSDK1504 |  Error   | Specified third-party notice file '...' was not found.  | A third-party notice file specified by the `PackageThirdPartyNoticeFile` property was not found in the project's directory nor in any containing directory.                                                                               |
| BVSDK1505 |  Error   | No third-party notice file found.                       | No third-party notice file was specified, and no default third-party notice file has been found. Set the `ThirdPartyNoticeInPackage` property to `false` to explicitly exclude any third-party notice file to be included in the package. |
| BVSDK1506 |  Error   | Specified package icon file '...' does not exist.       | A package icon file specified by the `PackageIconPath` property does not exist.                                                                                                                                                           |
| BVSDK1507 |  Error   | Specified package icon file '...' was not found.        | A package icon file specified by the `PackageIcon` property was not found in the project's directory nor in any containing directory.                                                                                                     |
| BVSDK1508 |  Error   | No package icon file found.                             | No package icon file was specified, and no default package icon has been found. Set the `IconInPackage` property to `false` to explicitly exclude any package icon to be included in the package.                                         |
| BVSDK1509 |  Error   | Specified README file '...' does not exist.             | A README file specified by the `PackageReadmePath` property does not exist.                                                                                                                                                               |
| BVSDK1510 |  Error   | Specified README file '...' was not found.              | A README file specified by the `PackageReadmeFile` property was not found in the project's directory nor in any containing directory.                                                                                                     |
| BVSDK1511 |  Error   | No README file found for package.                       | No README file was specified, and no default README file has been found. Set the `ReadmeFileInPackage` property to `false` to explicitly exclude any README file to be included in the package.                                           |

## ReferenceAssemblies module (1600-1699)

This module has no associated diagnostics.

## StandardAnalyzers module (1700-1799)

This module has no associated diagnostics.

## XmlDocumentation module (1800-1899)

This module has no associated diagnostics.

## AlternatePack module (1900-1999)

| Code      | Severity | Message                                                          | Description                                                                                                           |
| --------- | :------: | ---------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------- |
| BVSDK1900 |  Error   | InnoSetup item '...' does not specify a script.                  | An `InnoSetup` item has no `Script` metadata.                                                                         |
| BVSDK1901 |  Error   | InnoSetup script '...' referenced by '...' does not exist.       | An `InnoSetup` item's `Script` metadata refers to a non-existing file.                                                |
| BVSDK1902 |  Error   | InnoSetup item '...' refers to non-existent PublishFolder '...'. | An `InnoSetup` item has a `SourcePublishFolder` metadata, but no `PublishFolder` item exists with the specified name. |

## NerdbankGitVersioning module (2000-2099)

| Code      | Severity | Message                                    | Description                                                                                        |
| --------- | :------: | ------------------------------------------ | -------------------------------------------------------------------------------------------------- |
| BVSDK2000 |  Error   | Version specification JSON file not found. | A `version.json` or `.version.json` file for the project was not found within the repository root. |

## ReleaseAssetList module (2100-2199)

This module has no associated diagnostics.

## Wine module (2200-2299)

| Code      | Severity | Message                                                                                                       | Description                                                                                                                                                                                                                                                                                                          |
| --------- | :------: | ------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| BVSDK2200 |  Error   | One or more tools need Wine to run on this system, but no Wine invocation command has been defined: ...[;...] | One or more tools needed to build and/or distribute your project need [Wine](https://winehq.org) to run under a non-Windows operating system. In order to use Wine with Buildvana SDK, the `WineInvocationCommand` property must be set as explained in [the module documentation](./modules/Wine.md#configuration). |
