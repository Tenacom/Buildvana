# Internal-use properties

Buildvana SDK defines properties for its own use, with a `BV_` prefix that keeps them apart from the properties of MSBuild and the .NET SDK.
This page lists the ones a repository can meet, and what each one means.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Compatibility](#compatibility)
- [Project type](#project-type)
- [Dependency management](#dependency-management)
  - [`BV_PinDumpDirectory`](#bv_pindumpdirectory)
  - [`BV_SuppressTransitiveOverrides`](#bv_suppresstransitiveoverrides)

---

## Compatibility

The properties are not a contract.
A name, a value, or a meaning may change in any release, and a property may appear or disappear, without a breaking-change notice.

When a project of yours reads or sets one of them, [open an issue](https://github.com/Tenacom/Buildvana/issues/new/choose).
The property may need a public counterpart, or the need may point at a bug.

> [!NOTE]
> The page does not list every `BV_` property.
> Most of them serve one module, and stay inside it.

---

## Project type

Five properties say what kind of project Buildvana SDK is building.
`BeforeNETSdk.targets` computes them right after the project body, in the order of the table, and the first condition that holds wins.
At most one of them is `true`, and the others are `false`.

| Property                   | `true` when the project                           |
| -------------------------- | ------------------------------------------------- |
| `BV_IsFileBasedAppProject` | is a file-based app, marked by `FileBasedProgram` |
| `BV_IsNoTargetsProject`    | uses the `Microsoft.Build.NoTargets` SDK          |
| `BV_IsTestProject`         | sets `IsTestingPlatformApplication` to `true`     |
| `BV_IsLibraryProject`      | sets `OutputType` to `Library`                    |
| `BV_IsExeProject`          | sets `OutputType` to `Exe` or `WinExe`            |

`FileBasedProgram` is the property the .NET CLI sets in the project it generates for a [file-based app](sdk-modules/file-based-apps.md).
`IsTestingPlatformApplication` is the property `dotnet test` reads to find a test project under Microsoft.Testing.Platform.
Buildvana supports that platform alone, so the `IsTestProject` property of VSTest plays no part.

The properties exist in the files MSBuild reads after the project body: the `.targets` files of the modules, and `BeforeCommon.targets`, `Common.targets`, and `AfterCommon.targets`.
The `.targets` files of the .NET SDK see them too, because Buildvana SDK computes them before the .NET SDK reads the project.
A `.props` file sees them empty.

The [`StandardAnalyzers`](sdk-modules/standard-analyzers.md) and [`XmlDocumentation`](sdk-modules/xml-documentation.md) modules read them to set the defaults of four properties.

| Project type               | `EnableNETAnalyzers` | `UseStyleCopAnalyzers` | `UsePublicApiAnalyzers` | `GenerateDocumentationFile` |
| -------------------------- | :------------------: | :--------------------: | :---------------------: | :-------------------------: |
| `BV_IsFileBasedAppProject` |        `true`        |         `true`         |         `false`         |           `false`           |
| `BV_IsNoTargetsProject`    |       `false`        |        `false`         |         `false`         |           `false`           |
| `BV_IsTestProject`         |        `true`        |         `true`         |         `false`         |           `false`           |
| `BV_IsLibraryProject`      |        `true`        |         `true`         |         `true`          |           `true`            |
| `BV_IsExeProject`          |        `true`        |         `true`         |         `false`         |           `false`           |

---

## Dependency management

`bv dependencies` passes two properties on the command line, and the [`Dependencies` module](sdk-modules/dependencies.md) reads them.

### `BV_PinDumpDirectory`

The directory the `BV_DumpPackagePins` target writes to.
The target writes the evaluated `PackageVersion`, `GlobalPackageReference`, and `PackageReference` items of the project, one file per project and target framework.
`bv dependencies` sets the property, runs the target over the projects of the solution, and reads the files back to see the package pins.

No other target depends on `BV_DumpPackagePins`, so a build never runs it.
The `WritePackagePinDump` task requires the directory, and raises error [BVSDK1050](sdk-diagnostics.md#buildvana-sdk-tasks-1050-1099) when the property is empty.

### `BV_SuppressTransitiveOverrides`

When the property is `true`, the `Dependencies` module does not import the [transitive override files](tool-commands/dependencies.md#transitive-overrides): `Directory.TransitiveOverrides.props` in the home directory, and `<ProjectName>.TransitiveOverrides.props` beside a project.
`bv dependencies` sets it to get NuGet's verdict on the dependency graph without the overrides, which is what it computes the next overrides from.
Suppressing the import spares `bv` from deleting the files and putting them back, which an interrupted run would leave undone.
