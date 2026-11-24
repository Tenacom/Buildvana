# Documentation inventory

This file is specific to this repository. Rewrite it when `.claude` is copied to another repository. `rules/common/documentation.md` holds the rules. This file lists what they apply to here: the files, the informal documents, the page kinds, the subfolders of `docs/`, the generated regions, the published documentation, and the pinned reference pages.

## Files

The following are documentation:

- `README.md`
- `CHANGELOG.md`
- `docs/`, which the documentation site renders
- `graphics/README.md`
- `src/<project>/NuGet-README.md`

The following are not documentation:

- `THIRD-PARTY-NOTICES.md` holds the copyright notices of code dependencies;
- `THIRD-PARTY-GRAPHICS.md` holds the copyright notices of the graphic elements in `graphics/`.

## Informal documents

None, beyond the `README.md` and `NuGet-README.md` files, which are informal documents in every repository. `docs/index.md`, the home page of the documentation site, follows the default register.

## Page kinds

Besides the guide and the reference of `rules/common/documentation.md`, the repository has the page kinds below.

A **module page** documents one SDK module, in the modules folder. Its title is the name of the module, and `ModulePagesTests` compares the two. Its level 2 headings come in this order, each present when the module has the content: "Configuration", for the properties and the `buildvana.jsonc` settings the user sets; "Usage", for items, tasks, and targets; "Diagnostics", for a link to the module's range in the SDK diagnostics page. A migration section, when the page has one, comes last.

A **command page** documents one `bv` command or command group, in the commands folder. Topic sections come first. "Options" and "Exit codes" come last, each present when the command has them.

The diagnostics pages are the model of a reference in table shape. The environment variables page is the model of a reference with one level 3 heading per item.

## Subfolders of `docs/`

The pages about Buildvana as a whole sit in `docs/` itself: the home page, the getting-started page, and the pages about the layout and the configuration of a repository. Every other page sits in one of these folders. The title of each folder is in its `_folder.yml`.

- `04-sdk/`, titled "Buildvana SDK": the pages about Buildvana SDK.
- `04-sdk/05-modules/`, titled "Modules": the modules folder, with one page per Buildvana SDK module.
- `05-bv/`, titled "bv": the pages about `bv`, the hooks included.
- `05-bv/05-commands/`, titled "Commands": the commands folder, with one page per `bv` command or command group.

A number prefix orders the pages of `docs/` itself and of the first two folders, as "Structure of the site" in `rules/common/documentation.md` says. The order is the one of a first reading. The modules folder and the commands folder hold reference pages, which sort by name and have no prefix.

## Generated regions

| Region             | Page            | Owner                                  |
| ------------------ | --------------- | -------------------------------------- |
| `TOOLCHAIN-FLOORS` | `docs/index.md` | `.buildvana/hooks/deps/post-update.cs` |

## Published documentation

The repository has a documentation site on GitHub Pages, with an API reference of `Buildvana.Runtime` alone. "Published documentation" in `rules/common/documentation.md` describes the site.

- The stable part is at `https://tenacom.github.io/Buildvana/`, and the preview part at `https://tenacom.github.io/Buildvana/preview/`. `Documentation.SiteUrl` in `tools/ProjectFacts.cs` holds the first URL.
- `Buildvana.Runtime` is the one package that `docgen.json` names, because it is the one library that the code of a repository references, in a hook. `bv` is a tool, and Buildvana SDK has no API that a project calls.
- `docs/index.md` is the home page, and the introduction to Buildvana. Its title is `title` in its front matter, and `site.homeTitle` in `docgen.json` names its entry in the navigation "Introduction".
- The title of every page is `title` in its front matter, and no page has a level 1 heading. The generator has one title per page, for the navigation, the heading, and the URL, so a title is short and leaves out what its folder says: "Wine", not "Wine module".
- Under the URL of a part, a page of `docs/` itself is `<title>.html`, with the title as the generator slugifies it, such as `getting-started.html`. A page of a folder follows the slugified titles of its folders, as in `buildvana-sdk/modules/wine.html`.
- The header of a transitive override file that `bv` writes links a page of the site by its URL. A change to the title of the page about `bv dependencies`, or of a folder above it, changes that URL. Keep the URL with `slug`, or change the link in the same commit.
- The page of a type is `api/<namespace>.<type>.html`, such as `api/Buildvana.Runtime.BuildvanaConfig.html`. The page of a namespace is `api/namespaces/<namespace>.html`. The page of a package is `api/packages/<package id>.html`, with the package id in lowercase.
- `dotnet run tools/docs.cs` builds the site into `.buildvana-temp/docs-site`, from the home directory. With `-- serve`, it also serves the site on `http://localhost:8080`, and builds it again on every change. Without the packages of `bv pack`, it builds the pages alone.
- The workflow `docs.yml` builds both parts and deploys the site. The `docs` job of `release.yml` calls it after `bv release`.
- The run of `ci.yml` that packs also runs the generator on the packages it built, with every warning as an error. A defect of an XML comment of `Buildvana.Runtime`, or of a page under `docs/`, therefore fails the checks of a pull request.

## Pinned reference pages

A test in the test project of the code pins each page below, as "Checks" in `rules/common/documentation.md` says.

- The module pages, against the directories under `src/Buildvana.Sdk/Modules`, by `ModulePagesTests`.
- The command pages, against `CommandRegistry`, by `CommandPagesTests`.
- The settings table of the configuration file page, against the schema `BuildvanaJsonConfigSchema` generates, by `ConfigurationPageTests`.
- The diagnostics page of Buildvana SDK, against the codes Buildvana SDK raises, by `SdkDiagnosticsPageTests`.
- The diagnostics page of `bv`, against `DiagnosticCodes`, one section per test project, by the `ToolDiagnosticsPageTests` of `Buildvana.Tool.Tests` and of `Buildvana.Core.Configuration.Tests`.

Each test names its page by path, so a page that moves or gets another number needs the new path in its test.

No test pins the API reference, because it comes from the code itself.
