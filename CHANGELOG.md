<!-- markdownlint-disable MD024 MD034 -->

# Changelog

All notable changes to Buildvana SDK will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Unreleased changes

### New features

### Changes to existing features

- `bv` may now be invoked from any subdirectory under the solution's directory (`HomeDirectory` property in Buildvana SDK). It will search upwards for the home directory, the same way the SDK does, and work from there. If it doesn't find the home directory, `bv` will exit with a non-zero exit code.
- **BREAKING CHANGE**: The `prepare` subcommand of `bv` has been renamed to `clean`, in order to maintain parallelism between `bv` and `dotnet` build pipeline subcommands (`restore`, `build`, etc.).
- **BREAKING CHANGE**: Microsoft Testing Platform (MTP) is the only supported test runner now. Test projects using VSTest are explicitly not supported and will cause test-time errors. Buildvana will assume all test projects to be built with MTP.
- **BREAKING CHANGE**: Code coverage reports are now collected in a `TestResults` directory at the repository root rather than under each test project's directory, and are no longer merged into a single file. Tooling that consumes coverage output (e.g. Codecov upload steps) must handle multiple *.cobertura.xml files; Codecov's GitHub Action accepts globs natively.
- **BREAKING CHANGE**: Test projects must set the `IsTestingPlatformApplication` MSBuild property to `true`.  In practice, all major MMTP-compatible test frameworks already do this through their SDK, so explicit assignment in project files is rarely needed. The old heuristic rule by which projects whose name ends in `.Tests` were considered test projects by default is no longer in effect. The `IsTestProject` property, used to identify VSTest test projects, is not checked.
- **BREAKING CHANGE**: `bv clean` (formerly known as `bv prepare`) now deletes the `TestResults` directory at the repository root.
- `bv clean` (formerly known as `bv prepare`) no longer deletes per-project `TestResults` directories.
- `dotnet bv release` no longer folds self-reference (dogfood) updates into the "Prepare release" commit. They now go into a separate `Update self-references to <version> [skip ci]` commit pushed on top, in the same push. The release tag binds to the "Prepare release" commit, so checking out the tag and rebuilding now reproduces the actually-released source state (which still references the previously-published versions). `[skip ci]` is required on the dogfood commit because the new packages are usually not yet published at push time.
- **BREAKING CHANGE**: Multi-word `bv` options no longer accept their camelCase aliases. Use the kebab-case form (e.g. `--main-branch` instead of `--mainBranch`, `--version-spec-change` instead of `--versionSpecChange`). The kebab-case form was already accepted alongside the camelCase one in previous previews.
- **BREAKING CHANGE**: Five `bv release` options have been renamed: `--versionSpecChange` → `--bump`, `--checkPublicApiFiles` → `--check-public-api`, `--updateChangelogOnPrerelease` → `--unstable-changelog`, `--ensureChangelogNotEmpty` → `--require-changelog`, `--updateSelfReferences` → `--dogfood`. The matching default-source environment variables follow the same renaming.

### Bugs fixed in this release

- Buildvana SDK now correctly checks the `IsTestingPlatformApplication` (required by MTP) instead of `IsTestProject` (required by VSTest) to determine whether a project is a test project and set `BV_IsTestProject` accordingly.

### Known problems introduced by this release

- When a release introduces no other file changes (typically a prerelease with no version-spec change and no changelog or public-API updates), the "Prepare release" commit is empty. The major public Git hosts accept empty commits, but self-hosted setups with custom `pre-receive` hooks may reject them. If this affects you, either allow empty commits, or set `--unstable-changelog` to `true` so prerelease runs always have a non-empty release commit. If neither option is acceptable for your workflow, please open an issue.
- The check for `IsTestProject` is wrong, as it's a VSTest-related property. MTP uses `IsTestingPlatformApplication` instead.

## [1.1.10](https://github.com/Tenacom/Buildvanareleases/tag/1.1.10) (2026-04-27)

### Bugs fixed in this release

- [Issue #243](https://github.com/Tenacom/Buildvana/issues/243) — `dotnet bv release` now rewrites `global.json`, `.config/dotnet-tools.json`, and `version.json` in place via a byte-level splice, preserving line endings, trailing newlines, indentation, comments, and BOM exactly as they were on disk. Previously, automatic dogfooding (and version-file updates) re-serialized the entire JSON document, producing all-lines-changed diffs and dropping the trailing newline.

## [1.1.4](https://github.com/Tenacom/Buildvanareleases/tag/1.1.4) (2026-04-27)

### New features

- `dotnet bv release` now updates `global.json`, `.config/dotnet-tools.json`, and `Directory.Packages.props` references to packages produced by the current build, so a self-hosting (dogfooded) project picks up the new version as part of the "Prepare release" commit.
As a consequence, checking out a version tag and rebuilding your repository's solution will **not** reproduce the build that was originally released — the tagged commit references the just-released SDK/tool versions, while the original release was built against the previously-published versions. Whether this matters depends on your project.
To skip automatic dogfooding, either pass `--updateSelfReferences=false` to `dotnet bv release`, or set the `UPDATE_SELF_REFERENCES` environment variable to `false` in your CI workflow.

### Bugs fixed in this release

- Repository links have been fixed: all references to READMEs, changelog, and the repository itself should now bear no trace of the old `Buildvana` organization and `Buildvana.Sdk` repository.

### Known problems introduced by this release

- [Issue #243](https://github.com/Tenacom/Buildvana/issues/243) — Automatic dogfooding, introduced with [PR #242](https://github.com/Tenacom/Buildvana/pull/242), does not preserve line endings, traling newlines, and any non-standard formatting in `global.json` and `.config/dotnet-tools.json` if it happens to modify them.

## [1.0.220](https://github.com/Tenacom/Buildvanareleases/tag/1.0.220) (2026-04-27)

### New features

- No more Cake build scripts:  Buildvana now has its own global CLI tool, `bv`. More info and documentation incoming.

### Changes to existing features

- The minimum supported version of the .NET SDK is now 10.0.202
- The minimum supported version of Roslyn is now 5.3
- The minimum supported version of Visual Studio is now VS2026 18.4
- Compiled tasks are now built for .NET 10 and [used out-of-process](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/sdk#use-net-msbuild-tasks-with-net-framework-msbuild) when MSBuild runs on .NET Framework.

### Bugs fixed in this release

- Additional assembly info generation failed on Visual Basic projects, because the source generator depended on `Microsoft.CodeAnalysis.CSharp`. It now depends on `Microsoft.CodeAnalysis.Common` instead, which is available in all Roslyn compilations.

## [1.0.154-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.154-preview) (2024-04-20)

### New features

- Buildvana SDK now supports loading a machine- and/or user-scoped configuration file named `Buildvana.Sdk.props`. Please refer to the relevant [documentation](docs/ConfigurationFiles.md) for details.
- A `.pfx` file used to sign an assembly through the `AssemblySigning` module can now have no password. Previous versions issued an error if the `PfxPassword` property was empty or not defined.
(Please note that the `PfxPassword` property has also been renamed to `AssemblyOriginatorKeyPassword`, as noted below in the "Changes to existing features" section.)
- Some support has been added for running Windows-only tools under [Wine](https://winehq.org) when building under Linux or macOS. Please refer to the relevant [documentation](docs/modules/Wine.md) for details.
- In Inno Setup support, when there is no `AssemblyTitle` property, `AppFullName` now defaults to `AssemblyName`.
- Inno Setup's compiler can now run on macOS and Linux (through Wine):
  - Wine support must be configured at machine / user level - please refer to the documentation for [configuration files](docs/ConfigurationFiles.md) and the [`Wine` module](docs/modules/Wine.md);
  - `InnoSetupConstant` items whose `Value` metadata is a filesystem path must have an `IsPath="true"` metadata, so that paths are converted to Windows-style paths when using Wine.

### Changes to existing features

- The minimum supported version of Roslyn is now 4.9
- The minimum supported version of Visual Studio is now VS2022 17.9
- The minimum supported version of the .NET SDK is now 8.0.200
- The `AssemblySigning` module now expects the password to use for the `.pfx` file in the `AssemblyOriginatorKeyPassword` (as opposed to `PfxPassword`) property.

### Bugs fixed in this release

- The `AssemblySigning` module did not work any more, due to the removal of `Buildvana.Sdk.Tasks.dll` in version 1.0.0-alpha.21. The compiled tasks have been brought back and are now compiled for .NET Standard 2.0, so that the same DLL can be used for both Visual Studio and the .NET SDK.
- Due to a typo in Inno Setup support code, `ReleaseAssetDescription` metadata for `InnoSetup` items were not honored and the default asset description was always used.
- `AppShortName` and `AppFullName` properties were not honored by Inno Setup support code; `AssemblyName` was used in their place.

## [1.0.131-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.131-preview) (2024-01-21)

This release just updates some dependencies to their latest versions, the most notable of them being `StyleCop.Analyzers`, brought up to 1.2.0-beta.556.

## [1.0.122-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.122-preview) (2023-11-22)

### Bugs fixed in this release

- Although the minimum supported Roslyn version was 4.8, version 1.0.116-preview still depended on version 4.7 of `Microsoft.CodeAnalysis.CSharp`. The dependency version has now been properly updated.

## [1.0.116-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.116-preview) (2023-11-17)

### Changes to existing features

- The minimum supported version of Roslyn is now 4.8
- The minimum supported version of Visual Studio is now VS2022 17.8
- The minimum supported version of the .NET SDK is now 8.0.100

## [1.0.110-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.110-preview) (2023-11-09)

### Changes to existing features

- The change in version 1.0.106-preview, whereas the `Title` property was used  as a default for `AssemblyTitle`, has been reversed. It turns out that the order in which MSBuild loads Buildvana.Sdk in relation to Microsoft.NET.Sdk makes the change ineffective.
- The default value for property `AppFullName`, used as a constant passed to InnoSetup, is now `$(AssemblyTitle)` instead of `$(Title)`.
- **BREAKING CHANGE**: The property used as NuGet package title is now `PackageTitle` instead of `Title`. If left unset, it defaults to `$(AssemblyTitle)`.

## [1.0.106-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.106-preview) (2023-11-09)

### Changes to existing features

- When using either NuGet Pack support or alternate pack, the `Title` property is now used as a default for `AssemblyTitle`.

## [1.0.102-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.102-preview) (2023-11-09)

### Changes to existing features

- When using NerdBack.GitVersioning, the value of `AssemblyInformationalVersion` is now changed to not include metadata (Git commit SHA) when building a public version (i.e. from `main` or other branches identified in `version.json`).
This change also affects the default names of zipped publish folders and InnoSetup-generated setup programs, as they use `AssemblyInformationalVersion` as a suffix.

## [1.0.99-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.99-preview) (2023-11-08)

### Bugs fixed in this release

- The separator character used in default InnoSetup output names before the program version was a minus sign `-` instead of its intended value of underscore `_`.

## [1.0.94-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.94-preview) (2023-11-04)

### Bugs fixed in this release

- InnoSetup would fail if the script specified in the `Script` metadata of an `InnoSetup` item was not in the same folder as the project.

## [1.0.88-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.88-preview) (2023-10-26)

### New features

- NuGet-related features provided by the `NuGetPack` module can now be disabled altogether by setting the `IncludeNuGetPackSupport` property to `false`. The default value is `true`, which behaves like previous versions.

### Changes to existing features

- **BREAKING CHANGE:** Alternate pack methods can now be used independently of one another. Therefore the `AlternatePackMethod` property has been discontinued; to enable alternate pack, just set `UseAlternatePack` to `true`, as in versions prior to 1.0.41-preview where `AlternatePackMethod` was introduced.
- **BREAKING CHANGE:** InnoSetup support has been completely rewritten. Main features include the following:
  - Creation of an InnoSetup installations is no longer bound to a `PublishFolder`.
  - An `InnoSetup` item must be created for every installation program to create.
  - Source files for an installation can come from a publish folders, or from a custom location.
  - Installation programs can be automatically added to the release asset list.
- The default name for a zipped publish folder is now suffixed with the complete informational version (the `AssemblyInformationalVersion` property) when available, including semantic versioning metadata. This allows for clearer distinction between, for example, zip files created locally and on a continuous integration server.

### Bugs fixed in this release

- It was not possible to run InnoSetup scripts for more than one runtime identifier with the same target framework. For example, given two `PublishFolder`s, both with `TargetFramework` set to `net7.0`, whose `RuntimeIdentifier`s were `win10-x86` and `win10-x64` respectively, a setup was generated only for the first of the two. This has been fixed.

## [1.0.75-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.75-preview) (2023-10-03)

### Changes to existing features

- If the `CreateZipFile` metadata of a `PublishFolder` item is `true` and its `ZipFileName` metadata is not set, the latter defaults to:
  - `$(MSBuildProjectName)-%(PublishFolder.Identity)_$(PackageVersion).zip` if the `PackageVersion` property is set
  (note that the `BuildVersion` and `AssemblyInformationalVersion` properties were previously used instead of `PackageVersion`);
  - `$(MSBuildProjectName)-%(PublishFolder.Identity).zip` otherwise
  (this has not changed).

## [1.0.72-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.72-preview) (2023-10-02)

### Changes to existing features

- If the `CreateZipFile` metadata of a `PublishFolder` item is `true` and its `ZipFileName` metadata is not set, the latter defaults to:
  - `$(MSBuildProjectName)-%(PublishFolder.Identity)_$(AssemblyInformationalVersion).zip` if the `AssemblyInformationalVersion` property is set
  (note that the `BuildVersion` property was previously used instead of `AssemblyInformationalVersion`);
  - `$(MSBuildProjectName)-%(PublishFolder.Identity).zip` otherwise
  (this has not changed).

## [1.0.69-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.69-preview) (2023-10-02)

### New features

- `ReleaseAsset` items can now have their MIME type specified via the `MimeType` metadata. When not specified, the MIME type of an asset defaults to `application/octet-stream`.
- Zipped publish folders may now have a `ReleaseAssetMimeType` metadata specifying their MIME type when uploaded as a release asset. The default value is `application/zip`.

### Changes to existing features

- **BREAKING CHANGE:** The format of release asset lists has changed: each line now contains the full path of an asset, its MIME type, and its description, separated (like before) by tab characters (Unicode U+0009).

## [1.0.66-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.66-preview) (2023-10-01)

### New features

- A new property `CompletePublishFolderMetadataDependsOn` has been added. The `CompletePublishFolderMetadata` target will depend on targets listed in this property. This is useful to separate concerns among alternate pack methods.
- The new `ReleaseAssetList` module allows for creation of lists of assets to associate with a release, useful when releases are created externally (GitHub, etc.) and associated assets are the only way to retrieve published artifacts.
  - release asset list generation is enabled by the `GenerateReleaseAssetList` boolean property, defaulting to `true` except in libraries and test projects;
  - to include a file in the release asset list for a project, just add one or more `ReleaseAsset` items;
  - the `Description` metadata of `ReleaseAsset` items can be used to add a textual description of each asset, for CI systems that can use it;
  - release assets without a `Description` metadata are given a default description according to the `DefaultReleaseAssetDescription` property, whose default value is "(no description given)";
  - release asset lists are UTF-8 text files;
    - each row of a release asset list contains the full path of an asset, a tab character (Unicode U+0009), and the asset's description;
    - rows are separated by the build system's line separator (CR+LF on Windows, LF otherwise);
  - each project in a solution generates its own release asset list, whose name can be set via the `ReleaseAssetListFileName` property, defaulting to `$(MSBuildProjectName).assets.txt`;
  - all release asset lists for a solution are placed in the artifacts directory, `$(ArtifactsDirectory)$(Configuration)`.
- New metadata in `PublishFolder` items allow for zipping a published folder:
  - `CreateZipFile` (boolean) enables the creation of a ZIP file with the contents of the published folder;
  - `ZipFileName` (string) is the name (complete with extension) of the created ZIP file;
  - ZIP files are created in the artifacts directory `$(ArtifactsDirectory)$(Configuration)`;
  - the same publish folder can be zipped _and_ used by InnoSetup, if needed;
  - if `Temporary` is set to `true` on a publish folder, it will be deleted after zipping (and after running InnoSetup if required);
  - zipped publish folders are added to the release asset list for the project by default, unless their `IsReleaseAsset` metadata is set to `false`;
  - `ReleaseAssetDescription` metadata can be set to the textual description for the zipped folder in the release asset list;
  - `CreateZipFile` defaults to `true` if `ZipFileName` is set, `false` otherwise;
  - if `CreateZipFile` is `true` and `ZipFileName` is not set, the latter defaults to:
    - `$(MSBuildProjectName)-%(PublishFolder.Identity)_$(BuildVersion).zip` if the `BuildVersion` property is set (such as when using Nerdbank.GitVersioning);
    - `$(MSBuildProjectName)-%(PublishFolder.Identity).zip` otherwise.

### Changes to existing features

- The minimum supported version of Roslyn is now 4.7
- The minimum supported version of Visual Studio is now VS2022 17.7
- The minimum supported version of the .NET SDK is now 7.0.401
- When not using `Nerdbank.GitVersioning`, a stub `GetBuildVersion` target is added to the project. This allows other targets to depend on `GetBuildVersion`. Care should be exercised, however, to check that version-related properties have actually being set.
- The `GetBuildVersion` target is always invoked before packing when using any alternate pack method. This ensures that properties such as `BuildVersion`, `InformationalVersion`, etc. are available to packing sub-modules, at least when using Nerdbank.GitVersioning.

### Bugs fixed in this release

- An item group called `InnoSetupIncludeLine`, used internally by the `AlternatePack` module when the `AlternatePackMethod` property is set to `InnoSetup`, was meant to be cleared after use to free up some memory, but wasn't actually cleared. This has been fixed.

## [1.0.51-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.51-preview) (2023-08-02)

### Changes to existing features

- The minimum supported version of Roslyn is now 4.6
- The minimum supported version of Visual Studio is now VS2022 17.6
- The minimum supported version of the .NET SDK is now 7.0.306
- The following automatically added dependencies have been updated:
  - `Jetbrains.Annotations` to version 2023.2.0
  - `Nerdbank.GitVersioning` to version 3.6.133
  - `StyleCop.Analyzers` to version 1.2.0-beta.507

## [1.0.41-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.41-preview) (2023-07-18)

### New features

- **BREAKING CHANGE:** The `UseAlternatePack` property is no longer recognized. Projects must instead set `AlternatePackMethod` to one of the following values:
  - `None`: does nothing (useful to silence warnings in library projects using `Microsoft.Net.Sdk.Web`)
  - `PublishToFolders`: publish to folders, no InnoSetup involved
  - `InnoSetup`: publish to folders and generate setup (this is the value to use in projects that previously set `UseAlternatePack` to `true`)

## [1.0.26-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.26-preview) (2023-05-02)

This version just updates all dependencies, as well as build scripts.

## [1.0.13-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.13-preview) (2022-11-25)

### Bugs fixed in this release

- Version 1.0.7-preview contained a syntax error in a `.targets` file.
- When using version 1.0.7-preview, Restore failed because of a missing Buildvana.Sdk v1.0.0 package.

## [1.0.7-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.7-preview) (2022-11-25) **_RETIRED_**

### New features

- The [`Nerdbank.GitVersioning`](https://github.com/dotnet/Nerdbank.GitVersioning) package is now automatically referenced if either a `version.json` or a `.version.json` file is found looking from the project directory up until `HomeDirectory`. To disable this behavior, set `UseNerdbankGitVersioning` to `false` either in your project file or in a `Common.props` file. To issue an error `BVSDK2000` if a version JSON file is _not_ found, set `UseNerdbankGitVersioning` to `true` either in your project file or in a `Common.props` file.

### Changes to existing features

- Errors and warnings issued by Buildvana SDK are no longer prefixed differently: `BVSDK` is the new prefix for all diagnostics.

### Known problems introduced by this release

- This version contains a syntax error in a `.targets` file that somehow slipped through to distribution.
- Because of a weird interaction between the `Microsoft.Build.NoTargets` SDK and `Nerdbank.GitVersioning` when packing without building (which our CI workflows, as luck would have it, do) this version will try to reference version 1.0.0 of itself - which still doesn't exist - and restore will fail every time.

## [1.0.0-alpha.23](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.23) (2022-11-13)

### New features

- TFM-specific public API files (`PublicAPI\$(TargetFramework)\PublicAPI.{Shipped|Unshipped}.txt`) can now be disabled for multi-target projects by setting the `UseTfmSpecificPublicApiFiles` property to `false`. They can also be enabled for non-multi-target files by setting the same property to `true`.

## [1.0.0-alpha.22](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.22) (2022-09-22)

### New features

- Quite a few more properties are exported to the external `NuSpecFile`. The complete list can be seen [in the source code](https://github.com/Tenacom/Buildvana/blob/main/src/Buildvana.Sdk/Modules/NuGetPack/NuspecFile.targets). A notable addition is `configuration`, the only default "nuspec property" missing in Buildvana SDK so far.
- Files used for the generation of a NuGet package are now shown in Visual Studio's Solution Explorer tree view, under a "virtual" folder named "- Package". This includes: the license file, the third-party notice file, the Readme file, the package icon, and the `NuspecFile` if specified either explicitly or implicitly (i.e. by having a `{ProjectName}.nuspec` file in the project folder).
- When using public API analyzers, [TFM-specific public API files](https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/PublicApiAnalyzers.Help.md#conditional-api-differences) are added to the project automatically.

### Changes to existing features

- When generating a NuGet package, previous versions of Buildvana SDK wrote messages to the build log specifying the full paths of license, third-party notice, readme, and icon files. These messages have been removed in favor of showing the files in Visual Studio's Solution Explorer.

### Bugs fixed in this release

- When using an external `.nuspec` file, the `$configuration$` tag did not work in previous versions of Buildvana SDK. This has been fixed.
- When using an external `.nuspec` file, the `$repositoryType$` tag did not work in previous versions of Buildvana SDK. This has been fixed.

## [1.0.0-alpha.21](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.21) (2022-09-20)

### New features

### Changes to existing features

- https://github.com/Tenacom/Buildvana/pull/158 - **BREAKING CHANGE:** The LiteralAssemblyAttributes module has been removed. The `CLSCompliant` and `ComVisible` properties, however, are still supported: the corresponding assembly attributes are generated by a source generator.
- https://github.com/Tenacom/Buildvana/pull/158 - **BREAKING CHANGE:** The ThisAssemblyClass module has been removed. The recommended workaround is to use the [`ThisAssembly`](https://www.clarius.org/ThisAssembly) package.
- https://github.com/Tenacom/Buildvana/pull/163 - **BREAKING CHANGE:** Buildvana SDK does not use or recognize a version file, or otherwise determine a project's version, any longer. The suggested workaround is to use [NerdBank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning#readme), [GitVersion](https://gitversion.net), or any other similar tool.

## [1.0.0-alpha.20](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.20) (2022-05-12)

### New features

- https://github.com/Tenacom/Buildvana/pull/142 - For packable projects, Buildvana SDK will automatically find a README.md file and include it in the NuGet package. To disable this feature, set the `ReadmeFileInPackage` property to `false`.
Recognized names for the README file, in order of lookup, are: `Package-README.md`; `package-readme.md`; `NuGet-README.md`; `nuget-readme.md`; `NuGet.md`; `nuget.md`; `README.md`; `readme.md`.

### Changes to existing features

- https://github.com/Tenacom/Buildvana/pull/146 - **BREAKING CHANGE:** The Polyfills module, introduced in v1.0.0-alpha.18, has been removed.
Polyfills are a complicated topic, with lots of edge cases. They are best dealt with at a project level. The experience acquired with the Polyfills module has helped shape a polyfill library that will be open-sourced shortly (and is, needless to say, built with Buildvana SDK).
**EDIT:** [PolyKit](https://github.com/Tenacom/PolyKit#readme) has born and is even better than anticipated!

## [1.0.0-alpha.19](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.19) (2022-04-29)

### Bugs fixed in this release

- https://github.com/Tenacom/Buildvana/pull/138 - The `UsePolyfills` property was forced to `true` in all projects in version 1.0.0-aplha.18.

## [1.0.0-alpha.18](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.18) (2022-04-26)

### New features

- https://github.com/Tenacom/Buildvana/pull/135 - Buildvana SDK will, by default, include in every C# project some polyfills that let developers use latest C# features on older platforms. To disable this feature set the `UsePolyfills` property to `false`.
  - Polyfills are provided by adding a reference to the following NuGet Packages:
    - [IndexRange](https://www.nuget.org/packages/IndexRange/);
    - [Nullable](https://www.nuget.org/packages/Nullable/).
  - In addition, the following classes are added to the project on platforms where they are not part of the BCL:
    - [System.Runtime.CompilerServices.CallerArgumentExpressionAttribute](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.callerargumentexpressionattribute)
    - [System.Runtime.CompilerServices.IsExternalInitAttribute](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.isexternalinitattribute)
    - [System.Runtime.CompilerServices.SkipLocalsInitAttribute](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.skiplocalsinitattribute)
    - [System.Diagnostics.StackTraceHiddenAttribute](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.stacktracehiddenattribute) - This one is excluded from release builds, as it would have no effect anyway; it is here just to avoid preprocessor conditionals in multi-targeted projects.
    - [ValidatedNotNullAttribute](https://docs.microsoft.com/en-us/dotnet/api/microsoft.validatednotnullattribute) - This attribute, as included by Buildvana SDK, does not have a namespace and thus does not require any `using` directive. Since a lot of projects already define this attribute, and to prevent conflicts with the Visual Studio SDK, you can disable the inclusion of this attribute buy setting the `UseValidatedNotNullAttribute` property to `false`.

## [1.0.0-alpha.17](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.17) (2022-04-24)

### New features

- https://github.com/Tenacom/Buildvana/pull/132 - InnoSetup integration now automatically includes Inno Download Plugin.
- https://github.com/Tenacom/Buildvana/pull/132 - Buildvana SDK can now be used outside of a Git repository: just put a file named ".buildvana-home" in the home directory (usually the same directory as your solution file). The ".buildvana-home" file is searched for before looking for a Git submodule or repository.

### Bugs fixed in this release

- https://github.com/Tenacom/Buildvana/pull/130 - InnoSetup integration has been fixed.

## [1.0.0-alpha.16](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.16) (2022-04-01)

### Bugs fixed in this release

- When using Buildvana SDK v1.0.0-alpha.14 with .NET SDK 6.0 and using ReSharper annotations, .NET SDK 5.0 was required too, because it was needed by the `Resharper.ExportAnnotations` dependency. This version updates `Resharper.ExportAnnotations` to a version that works with .NET SDK 6, thus removing the aforementioned requirement.

## [1.0.0-alpha.15](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.15) (2022-04-01)

### New features

- https://github.com/Tenacom/Buildvana/pull/124 - Alternate Pack target: use the Pack target to publish to one or more folders and/or create setup executables with InnoSetup. See the PR for more information.

### Changes to existing features

- https://github.com/Tenacom/Buildvana/pull/123 - **POTENTIALLY BREAKING CHANGE:** The minimum supported MSBuild version is now 17.0 (.NET SDK 6.0, Visual Studio 2022 v17.0).
- https://github.com/Tenacom/Buildvana/pull/123 - **POTENTIALLY BREAKING CHANGE:** The only supported .NET environments are now .NET 6.0 or newer and .NET Framework 4.7.2 or newer. This of course refers to the build phase; you can use Buildvana SDK to target older versions of .NET, .NET Core, or .NET Framework.
- https://github.com/Tenacom/Buildvana/pull/123 - **POTENTIALLY BREAKING CHANGE:** The `AllowUnderscoresInMemberNames` property is no longer supported. Just append `;CA1707` to the `NoWarn` property instead.

## [1.0.0-alpha.14](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.14) (2021-09-13)

### Changes to existing features

- Warning NU1604 is no longer suppressed on dependencies automatically introduced in projects by Buildvana SDK. Suppressing the warning prevented a yellow triangle from appearing near the affected packages in Visual Studio 2019 until version 16.7; in version 16.11, on the contrary, the yellow triangle appears if the warning _is_ suppressed.

## [1.0.0-alpha.13](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.13) (2021-09-12)

### Changes to existing features

- **POTENTIALLY BREAKING CHANGE:** The minimum supported MSBuild version is now 16.8 (.NET SDK 5.0, Visual Studio 2019 v16.8).
- **POTENTIALLY BREAKING CHANGE:** Building with .NET Core 3.1 SDK is not supported any longer.

### Bugs fixed in this release

- https://github.com/Tenacom/Buildvana/pull/98 - XML documentation files are now correctly created (regression in versions 1.0.0-alpha.10 through 12).

## [1.0.0-alpha.12](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.12) (2021-01-19)

### Bugs fixed in this release

- https://github.com/Tenacom/Buildvana/pull/74 - Projects using Buildvana SDK now work with Omnisharp in VS Code.

## [1.0.0-alpha.11](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.11) (2021-01-19)

### Bugs fixed in this release

- https://github.com/Tenacom/Buildvana/pull/72 - False-positive BVW1400 and/or BVW1900 warnings are not raised any more.
- https://github.com/Tenacom/Buildvana/pull/72 - Properties `GenerateAssemblyCLSCompliantAttribute` and `GenerateAssemblyComVisibleAttribute` are not set any more if `GenerateLiteralAssemblyInfo` is set to `false`.
- https://github.com/Tenacom/Buildvana/pull/72 - `LiteralAssemblyAttribute` items are not generated any more if `GenerateLiteralAssemblyInfo` is set to `false`.
- https://github.com/Tenacom/Buildvana/pull/72 - Warning CS3021 ("'type' does not need a CLSCompliant attribute because the assembly does not have a CLSCompliant attribute") is not suppressed any more if `GenerateLiteralAssemblyInfo` is set to `false`.
- https://github.com/Tenacom/Buildvana/pull/72 - Literal assembly attributes are now correctly regenerated if an attribute's named parameter changes.
- https://github.com/Tenacom/Buildvana/pull/72 - `WriteLiteralAssemblyAttributes` and `WriteThisAssemblyClass` tasks are now correctly unloaded after execution.

## [1.0.0-alpha.10](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.10) (2021-01-03)

### Changes to existing features

- **POTENTIALLY BREAKING CHANGE:** The minimum supported MSBuild version is 16.7 (.NET SDK 3.1, Visual Studio 2019 v16.7).
- **BREAKING CHANGE:** The syntax for parameters of literal assembly attributes, as well as constants in "ThisAssembly" classes, has changed. The new syntax is described in [this document](docs\ConstantsSyntax.md).
- **BREAKING CHANGE:** The `Microsoft.CodeAnalysis.FxCopAnalyzers` package is not imported any more, due to its deprecation in favor of `Microsoft.CodeAnalysis.NetAnalyzers` (see [the relevant documentation](https://docs.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview) for more details).
- **BREAKING CHANGE:** The `UseStandardAnalyzers` property is not used any more. The new `UseStyleCopAnalyzers` property enables the use of the `StyleCop.Analyzers` package.
- https://github.com/Tenacom/Buildvana/pull/62 - Messages listing the icon, license file, and/or third-party copyright notice included in packages are now shown only when packing.
- https://github.com/Tenacom/Buildvana/pull/57 - Generated `ThisAssembly` classes now have [CompilerGenerated](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.compilergeneratedattribute) and [ExcludeFromCodeCoverage](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.codeanalysis.excludefromcodecoverageattribute) attributes.
- **BREAKING CHANGE:** https://github.com/Tenacom/Buildvana/pull/57 - The default for the `UseJetBrainsAnnotations` property is now `false`. The reason is that it was counterintuitive to mention JetBrains annotations in projects _not_ using them.
- Compiled tasks used to generate ThisAssembly classes and literal assembly attributes have been completely rewritten using Roslyn code generators.
- The message for error `BVE1004` now reports the minimum required MSBuild version.
- The message for warning `BVW1900` ("ThisAssembly class generation is only supported in C# and Visual Basic projects") now reports the `Language` MSBuild property value for the project.
- **POTENTIALLY BREAKING CHANGE:** Errors `BVE1900` and `BVE1901` did not make sense with [the new constant syntax](docs\ConstantsSyntax.md). They have been removed, and the old error `BVE1902` is now `BVE1900`.

### Bugs fixed in this release

- https://github.com/Tenacom/Buildvana/pull/65 - Warning BVW1900 issued on every project with a `<TargetFrameworks>` property and ThisAssembly class generation enabled.

## [1.0.0-alpha.9](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.9) (2020-10-10)

### Changes to existing features

- https://github.com/Tenacom/Buildvana/pull/51 - The automatically-added package reference to `ReSharper.ExportAnnotations.Task` has been updated to version 1.3.1.
- **POTENTIALLY BREAKING CHANGE:** https://github.com/Tenacom/Buildvana/pull/51 - The `EnableThisAssemblyClass` property has been renamed to `GenerateThisAssemblyClass` and its default value is now `false`.

### Bugs fixed in this release

- Thanks to the `ReSharper.ExportAnnotations.Task` update, building a project on a non-Windows system will no longer fail. See https://github.com/tenacom/ReSharper.ExportAnnotations/issues/23 for details.

## [1.0.0-alpha.8](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.8) (2020-10-10)

### Changes to existing features

- https://github.com/Tenacom/Buildvana/pull/47 - The automatically-added package reference to `ReSharper.ExportAnnotations.Task` has been updated to version 1.3.0.
- https://github.com/Tenacom/Buildvana/pull/49 - Compiled tasks are built for more target frameworks, to cover a larger number of build environments and MSBuild / .NET (Core) / Visual Studio versions.

### Bugs fixed in this release

- Thanks to the `ReSharper.ExportAnnotations.Task` update, building a project with `dotnet build` using .NET Core SDK v3.1 or .NET SDK 5-rc1 does not require.NET Core 2.1 to be installed any longer. See https://github.com/tenacom/ReSharper.ExportAnnotations/issues/20 for details.

## [1.0.0-alpha.7](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.7) (2020-09-28)

### New features

- https://github.com/Tenacom/Buildvana/issues/41 - Buildvana SDK now uses compiled tasks instead of inline tasks, thus improving build performance.
- https://github.com/Tenacom/Buildvana/issues/43 - Setting the `EnableDefaultThisAssemblyConstants` property to `false` suppresses creation of default constants in the `ThisAssembly` class.
- Warning [BVW1400] is now issued if literal assembly attribute generation is enabled for a project in a language that is neither C# nor Visual Basic. Previous versions silently skipped the code generation phase.
- Warning [BVW1900] is now issued if `ThisAssembly` class generation is enabled for a project in a language that is neither C# nor Visual Basic. Previous versions silently skipped the code generation phase.

### Changes to existing features

- **POTENTIALLY BREAKING CHANGE:** https://github.com/Tenacom/Buildvana/issues/44 - The `AssemblyInfo` module has been removed. Assembly attribute generation-related properties like e.g. `GenerateAssemblyInfo`, `GenerateAssemblyVersionAttribute`, etc. are not set to `true` any more at project and common files evaluation time; instead, they are left unset and defaulted to `true` later.
- **POTENTIALLY BREAKING CHANGE:** [Errors and warnings](docs/ErrorsAndWarnings.md) have been renumbered.
- **BREAKING CHANGE:** https://github.com/Tenacom/Buildvana/issues/44 - The `CLSCompliant` property is no longer set to `true` by default; it must be set explicitly in order to generate the respective assembly attribute. Projects that contain `CLSCompliant` attributes on types and members and do not set the `CLSCompliant` property will now issue warning CS3021: _'<type_or_member>' does not need a CLSCompliant attribute because the assembly does not have a CLSCompliant attribute._. To avoid the warning, set the `CLSCompliant` property to `true` (the previous default) in the project file or in a common file.
- **BREAKING CHANGE:** https://github.com/Tenacom/Buildvana/issues/44 - The `ComVisible` property is no longer set to `false` by default; it must be set explicitly in order to generate the respective assembly attribute. In projects that need to have all types and members of the compiled assembly hidden from COM, now you must set the `ComVisible` property to `false` (the previous default) in the project file or in a common file.

### Bugs fixed in this release

- https://github.com/Tenacom/Buildvana/issues/42 - The `ThisAssembly` class was never generated by previous versions of Buildvana SDK.

### Known problems introduced by this release

## [1.0.0-alpha.6](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.6) (2020-09-19)

### New features

- https://github.com/Tenacom/Buildvana/issues/35 - A package reference to `Microsoft.NETFramework.ReferenceAssemblies` is automatically added to projects targeting .NET Framework so they can be built on non-Windows systems, or without a .NET Targeting  Pack installed.

### Bugs fixed in this release

- https://github.com/Tenacom/Buildvana/issues/36 - Building projects with [centrally-managed package versions](https://stu.dev/managing-package-versions-centrally) now works.

## [1.0.0-alpha.5](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.5) (2020-09-17)

### Bugs fixed in this release

- Dependency `ReSharper.ExportAnnotations` has been updated to version 1.1.0. This release fixes two rather serious bugs that affected Buildvana SDK's functionality. See [their changelog](https://github.com/tenacom/ReSharper.ExportAnnotations/blob/main/CHANGELOG.md) for more information.

## [1.0.0-alpha.4](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.4) (2020-09-14)

### Changes to existing features

- https://github.com/Tenacom/Buildvana/issues/30 - The LiteralAssemblyAttributes module now works as expected.

## [1.0.0-alpha.3](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.3) (2020-09-14)

### New features

- **POTENTIALLY BREAKING CHANGE:** https://github.com/Tenacom/Buildvana/issues/26 - A unit test project is now recognized as such, by convention, if its name ends with `.Tests`.
  To opt out of this convention, explicitly set `IsTestProject` to `true` or `false`.

### Changes to existing features

- Dependency `StyleCop.Analyzers` has been updated to version 1.2.0-beta.205

## [1.0.0-alpha.2](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.2) (2020-09-13)

### New features

- https://github.com/Tenacom/Buildvana/issues/22 - Warning CA1707 (Identifiers should not contain underscores) is now suppressed by default in test projects. You can control this feature via the `AllowUnderscoresInMemberNames` property.

## [1.0.0-alpha.1](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.1) (2020-09-12)

Initial release.
