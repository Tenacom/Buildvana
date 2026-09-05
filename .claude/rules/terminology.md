# Terminology

One name per thing, in every kind of prose: documentation, chat, commit messages, issues, and code comments. `output-styles/simple-tech.md` states the rule. This file holds the names. When a thing has no name here, add one before using it in a second place.

| Thing                                                             | Name                | Not                                                |
| ----------------------------------------------------------------- | ------------------- | -------------------------------------------------- |
| The project, and the family of packages                           | Buildvana           | the Buildvana project                              |
| The MSBuild SDK                                                   | Buildvana SDK       | the SDK; `Buildvana.Sdk`, except as the package id |
| The CLI tool                                                      | `bv`                | the tool, Buildvana CLI tool, the Buildvana tool   |
| The shared library package                                        | `Buildvana.Runtime` | the runtime                                        |
| The directory Buildvana works from                                | home directory      | repository root, solution directory                |
| The configuration file, in either spelling                        | `buildvana.jsonc`   | `buildvana.json`, the configuration file           |
| A `.buildvana/hooks/<context>/<event>.cs` file                    | hook                |                                                    |
| A `bv` verb, such as `build`                                      | command             | subcommand                                         |
| A verb under a command group, such as `show` under `dependencies` | subcommand          |                                                    |
| A directory under `src/Buildvana.Sdk/Modules/`, such as `Wine`    | module              |                                                    |

`buildvana.jsonc` stands for either spelling because the name reminds the reader that comments and trailing commas are accepted. The page about the configuration file says that `buildvana.json` is accepted too.
