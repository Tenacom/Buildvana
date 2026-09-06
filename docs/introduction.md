# Introduction

Buildvana is a build system for .NET projects, built on MSBuild and Roslyn.
This page says what Buildvana is made of, what it does for a project, and what it runs on.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Packages](#packages)
- [Benefits](#benefits)
- [Compatibility](#compatibility)
  - [Project types](#project-types)
  - [Programming languages](#programming-languages)
  - [Git servers](#git-servers)
  - [Toolchain](#toolchain)

---

## Packages

Buildvana ships as three packages.

- Buildvana SDK, package id `Buildvana.Sdk`, is an MSBuild SDK that works alongside the SDK a project specifies.
- `bv`, package id `bv`, is a .NET CLI global tool that wraps common MSBuild targets and higher-level build operations, such as releasing a version and updating dependencies.
- `Buildvana.Runtime` is a library holding the typed model of the Buildvana configuration, and of the run-time information `bv` passes to [hooks](hooks.md).

---

## Benefits

- Buildvana SDK keeps project files short.
  A project states what differs from the defaults, and the defaults cover more than those of a plain MSBuild SDK.
- A plain-text `VERSION` file states the version of every project.
  The [`Versioning` module](sdk-modules/versioning.md) computes the patch number from the Git height.
- One place states the package license and the copyright notice of every project.
- Buildvana SDK generates assembly attributes beyond the ones the .NET SDK generates, such as `CLSCompliant` and `ComVisible`.
- Buildvana SDK configures commonly used code analyzers.

---

## Compatibility

Each table below states a status for every item.
"Supported" means that Buildvana works with the item.
"Supported with limits" means the same, with the limit stated in the table.
"Untested" means that nobody has reported using Buildvana with the item, and that testers are welcome.
"Unsupported" means that Buildvana does not work with the item.

### Project types

| Project type                                       | Status      |
| -------------------------------------------------- | ----------- |
| Multi-platform and cross-platform projects         | Supported   |
| Libraries                                          | Supported   |
| Console apps                                       | Supported   |
| Windows Forms                                      | Supported   |
| ASP.NET                                            | Supported   |
| Projects using the `Microsoft.Build.NoTargets` SDK | Supported   |
| [Avalonia UI](https://avaloniaui.net)              | Supported   |
| WPF                                                | Untested    |
| [UNO Platform](https://platform.uno)               | Untested    |
| .NET MAUI                                          | Untested    |
| Legacy projects, with no `Sdk` attribute           | Unsupported |

### Programming languages

| Language        | Status                | Limits                      |
| --------------- | --------------------- | --------------------------- |
| C#              | Supported             |                             |
| Visual Basic    | Unsupported           |                             |
| F#              | Supported with limits | Some features are disabled. |
| Other languages | Unsupported           |                             |

### Git servers

| Server                       | Status                | Limits                                       |
| ---------------------------- | --------------------- | -------------------------------------------- |
| GitHub and GitHub Enterprise | Supported             |                                              |
| Other servers                | Supported with limits | Buildvana SDK does not configure SourceLink. |

### Toolchain

The table lists the minimum version of each tool that can build a project with Buildvana.

<!-- TOOLCHAIN-FLOORS:START -->

| Tool          | Minimum version |
| ------------- | --------------- |
| .NET SDK      | 10.0.400        |
| Visual Studio | 2026 18.9       |
| MSBuild       | 18.9            |

<!-- TOOLCHAIN-FLOORS:END -->

MSBuild ships with Visual Studio and with the .NET SDK.
A Visual Studio version that meets its minimum ships an `msbuild.exe` that meets the MSBuild minimum.
A .NET SDK version that meets its minimum ships a `dotnet msbuild` that meets it.
Building from JetBrains Rider is untested.
