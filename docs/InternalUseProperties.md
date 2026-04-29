# Internal-use properties

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Overview](#overview)
- [Project type](#project-type)
  - [`BV_IsExeProject`](#bv_isexeproject)
  - [`BV_IsLibraryProject`](#bv_islibraryproject)
  - [`BV_IsTestProject`](#bv_istestproject)
  - [`BV_IsNoTargetsProject`](#bv_isnotargetsproject)

## Overview

Buildvana SDK introduces some properties only meant for use by the SDK itself. These properties are prefixed with `BV_` to avoid polluting the already crowded MSBuild property namespace.

**These properties are not meant for use outside of the SDK.** Any change in their names, contents, and/or semantics, as well as the addition and/or removal of internal-use properties, _will not be considered a breaking change_.

If you find yourself referring to or modifying any of these properties in your own projects, please [open an issue](https://github.com/Tenacom/Buildvana/issues/new/choose). Chances are we need to either change one or more properties to well-known properties, or otherwise fix what is probably a bug.

> **NOTE:** Not all `BV_`-prefixed properties are documented here, as most of them are use internally by single SDK modules.

## Project type

This group of properties, all starting with `BV_Is`, are flags that identify various types of project (library, executable, test project, etc.).

> **NOTE:** Since some of the project properties we need to determine the values of `BV_Is` properties are not available when `Sdk.props` is included, this group of properties is only available in "post-project" files, including:
>
> - `BeforeModules.targets`, `Module.targets`, and `AfterModules.targets` in SDK modules;
> - `BeforeCommon.targets`, `Common.targets`, and `AfterCommon.targets` in solution directories and subdirectories, although their use here is strongly discouraged.

### `BV_IsExeProject`

This property is `true` if the project produces an executable, `false` otherwise.

By default, standard analyzers are enabled and public API analyzers are disabled for this type of project.

By default, XML documentation generation is disabled for this type of project.

### `BV_IsLibraryProject`

This property is `true` if the project produces a library, and (by default) a corresponding NuGet package.

By default, both standard analyzers and public API analyzers are enabled for this type of project.

By default, XML documentation generation is enabled for this type of project.

### `BV_IsTestProject`

This property is `true` if the `IsTestingPlatformApplication` property is `true`, `false` otherwise.

`IsTestingPlatformApplication` has to be `true` in order for `dotnet test` to recognize test projects when using the Microsoft Testing Platform. As Buildvana only supports MTP for tests, VSTest's `IsTestProject` property is not considered when computing `BV_IsTestProject`.

By default, standard analyzers are enabled and public API analyzers are disabled for this type of project.

By default, XML documentation generation is disabled for this type of project.

### `BV_IsNoTargetsProject`

This property is `true` if the project uses the `Microsoft.Build.NoTargets` SDK. One example of this type of project is Buildvana SDK itself.

By default, both standard analyzers and public API analyzers are disabled for this type of project.

By default, XML documentation generation is disabled for this type of project.
