# `StandardAnalyzers` module

This module turns on the code quality and code style analyzers of the .NET SDK, StyleCop, and the public API analyzers.
It sets stricter defaults than the .NET SDK, and adds the `stylecop.json` file and the public and internal API files to the compilation.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Configuration](#configuration)
  - [Code analysis properties](#code-analysis-properties)
  - [`UseStyleCopAnalyzers` property](#usestylecopanalyzers-property)
  - [`UsePublicApiAnalyzers` property](#usepublicapianalyzers-property)
  - [`UseTfmSpecificPublicApiFiles` property](#usetfmspecificpublicapifiles-property)
  - [`UseTfmSpecificInternalApiFiles` property](#usetfmspecificinternalapifiles-property)
- [Usage](#usage)
  - [Package references](#package-references)
  - [`stylecop.json` file](#stylecopjson-file)
  - [Public API files](#public-api-files)
  - [Internal API files](#internal-api-files)
- [Diagnostics](#diagnostics)

---

## Configuration

The module is active in every project, and each of the three analyzer sets has a switch of its own.
Configure the rules of every set in an `.editorconfig` file or a `.globalconfig` file, as the [.NET documentation](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files) describes.
A severity in an `.editorconfig` file reaches only a diagnostic located in a source file.
So configure RS0052, RS0053, RS0054, and RS0058 in a `.globalconfig` file, because they report a line of an internal API file, or no location.

### Code analysis properties

The module sets the defaults of four properties of the .NET SDK.

| Property                  | Default  | Meaning                                                                                                  |
| ------------------------- | -------- | -------------------------------------------------------------------------------------------------------- |
| `EnableNETAnalyzers`      | `true`   | Whether the code quality analyzers run.                                                                  |
| `AnalysisLevel`           | `latest` | The .NET release whose rule set the analyzers apply, and `latest` is the release of the .NET SDK in use. |
| `AnalysisMode`            | `All`    | Which rules of the set are on, and `All` turns every rule on as a warning.                               |
| `EnforceCodeStyleInBuild` | `true`   | Whether the code style analyzers run in a build.                                                         |

The .NET SDK alone runs the code quality analyzers for .NET 5 and later, in `Default` mode.
It applies the rule set of the target framework, and leaves the code style analyzers out of a build.

`EnableNETAnalyzers` is `false` for a project that uses the `Microsoft.Build.NoTargets` SDK.
An `EnableNETAnalyzers` value MSBuild reads as true, such as `yes`, counts as `true`, and any other value counts as `false`.
An `EnforceCodeStyleInBuild` value MSBuild reads as false, such as `no`, counts as `false`, and any other value counts as `true`.
The [code analysis properties](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#code-analysis-properties) section of the .NET SDK reference describes each property, and lists the values `AnalysisLevel` and `AnalysisMode` accept.

### `UseStyleCopAnalyzers` property

Set it to `false` to leave StyleCop out of a project.
The default is `true`, except for a project that uses the `Microsoft.Build.NoTargets` SDK.
A value MSBuild reads as true, such as `yes`, counts as `true`, and any other value counts as `false`.

### `UsePublicApiAnalyzers` property

Set it to `true` to run the public API analyzers in a project, or to `false` to leave them out.
The default is `true` for a [library project](../internal-use-properties.md#project-type), and `false` for every other project.
A value MSBuild reads as true, such as `yes`, counts as `true`, and any other value counts as `false`.

### `UseTfmSpecificPublicApiFiles` property

Set it to `true` to keep one pair of [public API files](#public-api-files) per target framework, or to `false` to keep one pair for the project.
The default is `true` for a project that sets `TargetFrameworks`, and `false` otherwise.
A value MSBuild reads as true, such as `yes`, counts as `true`, and any other value counts as `false`.

### `UseTfmSpecificInternalApiFiles` property

Set it to `true` to keep one pair of [internal API files](#internal-api-files) per target framework, or to `false` to keep one pair for the project.
The default is `true` for a project that sets `TargetFrameworks`, and `false` otherwise.
A value MSBuild reads as true, such as `yes`, counts as `true`, and any other value counts as `false`.

---

## Usage

### Package references

The module adds a `PackageReference` to `StyleCop.Analyzers` when `UseStyleCopAnalyzers` is `true`, and one to `Microsoft.CodeAnalysis.PublicApiAnalyzers` when `UsePublicApiAnalyzers` is `true`.
Buildvana SDK pins the version of each package, and `dotnet list package` shows the reference as auto-referenced, with the `(A)` mark.
Each reference has `PrivateAssets="All"`, so a project that references this one, and a NuGet package this one produces, get no dependency on the analyzers.
A `PackageReference` of yours to either package replaces the module's reference, and under central package management a `PackageVersion` item sets its version.

The code quality and code style analyzers ship with the .NET SDK, and need no package.

### `stylecop.json` file

StyleCop reads its settings from a `stylecop.json` file passed as an additional file of the compilation, and the [StyleCop documentation](https://github.com/DotNetAnalyzers/StyleCopAnalyzers/blob/master/documentation/Configuration.md) describes the settings.
The module finds the file and passes it, so that no project lists it.
It looks for `stylecop.json` from the project directory upward, and for `.stylecop.json` when there is none.
A file above the home directory does not count.
The nearest file wins, so a file in the home directory serves every project, and a file nearer a project replaces it for that project.
The file does not show in the project tree of an IDE.
A project with no such file runs StyleCop with its default settings.

### Public API files

The public API analyzers compare the public types and members of an assembly with two text files, `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`, and report every difference.
The [help page](https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/PublicApiAnalyzers.Help.md) of the analyzers says how to maintain the files.
The analyzer package reads the two files from the project directory.

When `UseTfmSpecificPublicApiFiles` is `true`, the public API may differ between target frameworks.
Put one pair of files in `PublicAPI/<TargetFramework>/` for each target framework, as in `PublicAPI/net10.0/PublicAPI.Shipped.txt`.
The module passes the pair of each target framework to the compilation of that framework.

### Internal API files

The public API analyzers can also compare the internal types and members of an assembly with `InternalAPI.Shipped.txt` and `InternalAPI.Unshipped.txt`.
The analyzer package does not read these two files, so the module passes them to the compilation.
When `UseTfmSpecificInternalApiFiles` is `false`, the module passes the pair in the project directory.
When `UseTfmSpecificInternalApiFiles` is `true`, put one pair of files in `InternalAPI/<TargetFramework>/` for each target framework, as in `InternalAPI/net10.0/InternalAPI.Shipped.txt`.
The module passes the pair of each target framework to the compilation of that framework.

Eight rules check the internal API files, and each is off by default.
Buildvana SDK does not turn them on.

| Rule   | Title                                                                  |
| ------ | ---------------------------------------------------------------------- |
| RS0051 | Add internal types and members to the declared API                     |
| RS0052 | Remove deleted types and members from the declared internal API        |
| RS0053 | The contents of the internal API files are invalid                     |
| RS0054 | Do not duplicate symbols in internal API files                         |
| RS0055 | Annotate nullability of internal types and members in the declared API |
| RS0056 | Enable tracking of nullability of reference types in the declared API  |
| RS0057 | Internal members should not use oblivious types                        |
| RS0058 | Missing shipped or unshipped internal API file                         |

To turn the eight rules on, add these lines to a `.globalconfig` file, as [Configuration](#configuration) says:

```text
dotnet_public_api_analyzer.require_api_files = true

dotnet_diagnostic.RS0051.severity = warning
dotnet_diagnostic.RS0052.severity = warning
dotnet_diagnostic.RS0053.severity = warning
dotnet_diagnostic.RS0054.severity = warning
dotnet_diagnostic.RS0055.severity = warning
dotnet_diagnostic.RS0056.severity = warning
dotnet_diagnostic.RS0057.severity = warning
dotnet_diagnostic.RS0058.severity = warning
```

Without `require_api_files`, RS0051 reports every internal type and member of a project that has no internal API file.
With `require_api_files` set to `true`, a project with neither file of a pair gets no diagnostic for that kind of API.
A project with one file of the internal pair still gets RS0058.

---

## Diagnostics

The module raises no diagnostic in its [StandardAnalyzers module (1700-1799)](../sdk-diagnostics.md#standardanalyzers-module-1700-1799) range.
