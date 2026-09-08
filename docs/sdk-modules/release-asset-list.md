# `ReleaseAssetList` module

This module writes the release asset list of a project, the text file that names the files [`bv release` uploads](../tool-commands/release.md#publishing) as release assets.
The [`AlternatePack` module](alternate-pack.md) adds its zip files and setup programs to the list, and a `ReleaseAsset` item adds any other file.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Configuration](#configuration)
  - [`GenerateReleaseAssetList` property](#generatereleaseassetlist-property)
  - [`ReleaseAssetDefaultMimeType` property](#releaseassetdefaultmimetype-property)
  - [`ReleaseAssetDefaultDescription` property](#releaseassetdefaultdescription-property)
  - [`ReleaseAssetListFileName` property](#releaseassetlistfilename-property)
  - [`ReleaseAssetListPath` property](#releaseassetlistpath-property)
- [Usage](#usage)
  - [`ReleaseAsset` items](#releaseasset-items)
  - [List file](#list-file)
- [Diagnostics](#diagnostics)

---

## Configuration

### `GenerateReleaseAssetList` property

Whether the module writes the list.
The default is `true`, except in a [library or test project](../internal-use-properties.md#project-type), where it is `false`.
The NuGet package of a library needs no list, because `bv release` uploads every package of the artifacts directory.
Set the property to `true` in a library that produces another file to release.

### `ReleaseAssetDefaultMimeType` property

The MIME type of an asset whose item states none.
The default is `application/octet-stream`.

### `ReleaseAssetDefaultDescription` property

The description of an asset whose item states none.
The default is empty, and an asset with no description keeps its file name on the release page.

### `ReleaseAssetListFileName` property

The name of the list file.
The default is `<project>.assets.txt`, where `<project>` is `$(MSBuildProjectName)`.
`bv release` reads every `*.assets.txt` file of the artifacts directory, so a name with another extension hides the list from it.

### `ReleaseAssetListPath` property

The path of the list file.
The default is `$(ArtifactsDirectory)$(Configuration)/<file name>`, where `$(ArtifactsDirectory)` is the [`artifacts\`](../directory-structure.md#artifacts) directory of the home directory.
`bv release` looks in that directory alone, so a path elsewhere hides the list from it.

---

## Usage

### `ReleaseAsset` items

A `ReleaseAsset` item names a file to release.
Its identity is the path of the file, and a relative path resolves against the project directory.
The list holds the full path.
The file does not have to exist when the list is written, and [`bv release` skips an asset it does not find](../tool-commands/release.md#publishing), with a warning.

| Metadata      | Default                                                                      | Meaning                                                                                   |
| ------------- | ---------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| `MimeType`    | [`ReleaseAssetDefaultMimeType`](#releaseassetdefaultmimetype-property)       | The content type GitHub serves the asset with.                                            |
| `Description` | [`ReleaseAssetDefaultDescription`](#releaseassetdefaultdescription-property) | The label of the asset on GitHub, which the release page shows in place of the file name. |

An item in the project file names the file by its path:

```xml
<ItemGroup>
  <ReleaseAsset Include="$(ArtifactsDirectory)$(Configuration)/MyApp.msi" MimeType="application/x-msi" Description="Windows installer" />
</ItemGroup>
```

A target that produces a file during the pack adds the item itself.
It runs before the list is written when `WriteReleaseAssetListDependsOn` names it:

```xml
<PropertyGroup>
  <WriteReleaseAssetListDependsOn>$(WriteReleaseAssetListDependsOn);BuildInstaller</WriteReleaseAssetListDependsOn>
</PropertyGroup>

<Target Name="BuildInstaller">
  <!-- Build the installer, then: -->
  <ItemGroup>
    <ReleaseAsset Include="$(ArtifactsDirectory)$(Configuration)/MyApp.msi" MimeType="application/x-msi" Description="Windows installer" />
  </ItemGroup>
</Target>
```

For the zip files and setup programs of the `AlternatePack` module, the [`PublishFolder`](alternate-pack.md#publishfolder-items) and [`InnoSetup`](alternate-pack.md#innosetup-items) items carry the MIME type and the description.

### List file

`dotnet pack` writes the list after the `Pack` target, whether the project is packable or not.
The list is a UTF-8 text file with one line per asset.
A line holds the full path of the file, its MIME type, and its description, separated by tabs.
When the project has no `ReleaseAsset` item, the module deletes the file instead, so a list from an earlier pack does not survive.

Every project of the solution writes its own list, and `bv release` reads them all.

---

## Diagnostics

The module raises no diagnostic in its [ReleaseAssetList module (2100-2199)](../sdk-diagnostics.md#releaseassetlist-module-2100-2199) range.
