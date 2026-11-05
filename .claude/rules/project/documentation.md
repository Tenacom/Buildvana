# Documentation inventory

This file is specific to this repository. Rewrite it when `.claude` is copied to another repository. `rules/common/documentation.md` holds the rules. This file lists what they apply to here: the files, the page kinds, the subfolders of `docs/`, the generated regions, the generated folders, and the pinned reference pages.

## Files

The following are technical documentation:

- `CHANGELOG.md`
- `docs/`

The following are not technical documentation. Check them for accuracy, but they are allowed a less formal register:

- `README.md`
- `graphics/README.md`
- `src/Buildvana.Sdk/NuGet-README.md`
- `src/Buildvana.Tool/NuGet-README.md`

The following are not documentation:

- `THIRD-PARTY-NOTICES.md` holds the copyright notices of code dependencies;
- `THIRD-PARTY-GRAPHICS.md` holds the copyright notices of the graphic elements in `graphics/`.

## Page kinds

Besides the guide and the reference of `rules/common/documentation.md`, the repository has two page kinds.

A **module page** documents one SDK module, under `docs/sdk-modules/`. Its level 2 headings come in this order, each present when the module has the content: "Configuration", for the properties and the `buildvana.jsonc` settings the user sets; "Usage", for items, tasks, and targets; "Diagnostics", for a link to the module's range in the SDK diagnostics page. A migration section, when the page has one, comes last.

A **command page** documents one `bv` command or command group, under `docs/tool-commands/`. Topic sections come first. "Options" and "Exit codes" come last, each present when the command has them.

The diagnostics pages are the model of a reference in table shape. The environment variables page is the model of a reference with one level 3 heading per item.

## Subfolders of `docs/`

- `sdk-modules/`: one page per Buildvana SDK module.
- `tool-commands/`: one page per `bv` command or command group.

## Generated regions

| Region             | Page                   | Owner                                  |
| ------------------ | ---------------------- | -------------------------------------- |
| `TOOLCHAIN-FLOORS` | `docs/introduction.md` | `.buildvana/hooks/deps/post-update.cs` |

## Generated folders

None.

## Pinned reference pages

A test in the test project of the code pins each page below, as "Checks" in `rules/common/documentation.md` says.

- The module pages, against the directories under `src/Buildvana.Sdk/Modules`, by `ModulePagesTests`.
- The command pages, against `CommandRegistry`, by `CommandPagesTests`.
- The settings table of `configuration-file.md`, against the schema `BuildvanaJsonConfigSchema` generates, by `ConfigurationPageTests`.
- `sdk-diagnostics.md`, against the codes Buildvana SDK raises, by `SdkDiagnosticsPageTests`.
- `tool-diagnostics.md`, against `DiagnosticCodes`, one section per test project, by the `ToolDiagnosticsPageTests` of `Buildvana.Tool.Tests` and of `Buildvana.Core.Configuration.Tests`.
