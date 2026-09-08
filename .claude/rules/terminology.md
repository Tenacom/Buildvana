# Terminology

One name per thing, in every kind of prose: documentation, chat, commit messages, issues, and code comments. `output-styles/simple-tech.md` states the rule. This file holds the names. When a thing has no name here, add one before using it in a second place.

| Thing                                                                                              | Name                | Not                                                |
| -------------------------------------------------------------------------------------------------- | ------------------- | -------------------------------------------------- |
| The project, and the family of packages                                                            | Buildvana           | the Buildvana project                              |
| The MSBuild SDK                                                                                    | Buildvana SDK       | the SDK; `Buildvana.Sdk`, except as the package id |
| The CLI tool                                                                                       | `bv`                | the tool, Buildvana CLI tool, the Buildvana tool   |
| The shared library package                                                                         | `Buildvana.Runtime` | the runtime                                        |
| The directory Buildvana works from                                                                 | home directory      | repository root, solution directory                |
| The configuration file, in either spelling                                                         | `buildvana.jsonc`   | `buildvana.json`, the configuration file           |
| A `.buildvana/hooks/<context>/<event>.cs` file                                                     | hook                |                                                    |
| A `bv` verb, such as `build`                                                                       | command             | subcommand                                         |
| A verb under a command group, such as `show` under `dependencies`                                  | subcommand          |                                                    |
| A directory under `src/Buildvana.Sdk/Modules/`, such as `Wine`                                     | module              |                                                    |
| A `bv` option accepted before or after the command name, such as `--verbosity`                     | global option       |                                                    |
| The commands `clean`, `restore`, `build`, `test`, and `pack`, which run in that order              | build pipeline      | build commands, pipeline commands                  |
| The arguments after `--` that `bv restore`, `bv build`, `bv test`, or `bv pack` passes to `dotnet` | forwarded arguments | pass-through arguments, extra arguments            |
| Running an invocation through the `bv` version that `.config/dotnet-tools.json` pins               | delegation          |                                                    |
| The `bv` version that `.config/dotnet-tools.json` pins                                             | pinned `bv`         | the manifest version, the local `bv`               |
| The check that `global.json` pins Buildvana SDK at the version of the running `bv`                 | SDK version check   | version check, SDK check                           |
| The commit `bv release` creates before the build, which the release tag names                      | release commit      | "Prepare release" commit                           |
| The commit `bv release` adds after the build, with the self-reference rewrites and hook changes    | post-release commit | dogfood commit                                     |
| The `<project>.assets.txt` file the `ReleaseAssetList` module writes                               | release asset list  | asset list, assets file                            |

`buildvana.jsonc` stands for either spelling because the name reminds the reader that comments and trailing commas are accepted. The page about the configuration file says that `buildvana.json` is accepted too.
