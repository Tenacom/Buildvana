# `ProjectEvaluation` module

This module writes the evaluated facts of a project to a file when `bv` asks for them.
`bv` reads the files of every project of the solution, and describes the projects to hooks.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Configuration](#configuration)
- [Usage](#usage)
  - [`BV_DumpProjectEvaluation` target](#bv_dumpprojectevaluation-target)

---

## Configuration

The module has no property of its own, and Buildvana SDK imports it into every project.
`bv` passes the [internal-use property](../internal-use-properties.md#project-evaluation) `BV_ProjectEvaluationDumpDirectory` on the command line, and the module reads it.
A build does not set the property, and nothing runs `BV_DumpProjectEvaluation`.

---

## Usage

### `BV_DumpProjectEvaluation` target

The target writes one file per project to the directory that [`BV_ProjectEvaluationDumpDirectory`](../internal-use-properties.md#bv_projectevaluationdumpdirectory) names.
`bv` sets the property, runs the target over the projects of the solution, and reads the files back.
No build target depends on `BV_DumpProjectEvaluation`, so a build never runs it.

The file states the facts of the project and of each of its target frameworks.
Each fact comes from the evaluation of the build that uses it:

- `IsPackable`, `PackAsTool`, and `PackageId` come from the evaluation that `dotnet pack` uses.
  For a multi-targeting project, that evaluation is the outer build.
- `TargetPath`, the documentation file, `OutputType`, and `IsTestProject` come from the evaluation for each target framework.
  NuGet imports the build files of a package into that evaluation alone, so a package can set `OutputType` or `IsTestProject` there.

The documentation file is the copy of the XML documentation file next to `TargetPath`.
It is absent when the project generates no documentation file.
