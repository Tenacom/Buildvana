# The Buildvana configuration file

`buildvana.jsonc` holds the settings of a repository that uses Buildvana.
`bv` and Buildvana SDK read it from the home directory.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Discovery](#discovery)
- [Resolution](#resolution)
- [Validation](#validation)
- [Settings](#settings)
- [Related files](#related-files)
- [Migration from the `.buildvana-home` marker](#migration-from-the-buildvana-home-marker)

---

## Discovery

The file is named `buildvana.jsonc` or `buildvana.json`.
Comments and trailing commas are accepted under either name.
A home directory that holds both is an error, [BVSDK1005](sdk-diagnostics.md#buildvana-sdk-core-1000-1049) in Buildvana SDK.

The file is a home-directory marker.
Home-directory discovery stops at the nearest directory that holds one, and [Location of the home directory](directory-structure.md#location-of-the-home-directory) lists the other markers.
An empty JSON object, `{}`, is valid content, and marks a directory without configuring anything.

`bv` reads the file before running any command.
Buildvana SDK reads it when the [`Versioning` module](sdk-modules/versioning.md) computes the version of a project.
A hook reads the settings of its run from [its args](hooks.md#the-repository-configuration), and not from the file.

---

## Resolution

Every setting has a built-in default, and a repository with no configuration file runs on the defaults.
A setting the file states replaces its default.
Three settings also have a command-line option, and the option replaces both:

| Option                  | Setting                                            |
| ----------------------- | -------------------------------------------------- |
| `-c`, `--configuration` | `dotnet.configuration` and `release.configuration` |
| `--check-public-api`    | `release.checkPublicApi`                           |
| `--dogfood`             | `release.dogfood`                                  |

A blank or all-whitespace string is not a value.
A blank optional setting counts as not stated, so the next tier applies.
A blank option value counts as no value, as [Options and arguments](command-line.md#options-and-arguments) says.

Three settings fall back to another setting instead of a fixed default:

- `release.configuration` falls back to `dotnet.configuration`.
- `nuget.feeds.prerelease` falls back to `nuget.feeds.release`.
- The `policy` of an additional package group falls back to `dependencies.scopes.packages`.

---

## Validation

`bv` validates the file against [its JSON schema](#related-files) when it loads it.
Each problem is reported as one of the [configuration diagnostics](tool-diagnostics.md#configuration-1100-1199), BV1100 to BV1108, at its location in the file.
An unknown property, a value of the wrong type, a value outside the allowed ones, and a repeated property name are errors.

A stated section states its required members.
A feed states `source` and `apiKeyEnv`, `git.identity` states `name` and `email`, and an additional package group states `files` and `items`.
A required member that is blank is an error too.

A file with any error fails every command, `clean` included, because `bv` reads the file before running a command.

The file holds no secret.
A setting that names a secret names the environment variable that carries it, and `bv` reads the variable where it uses the value.
[Secret-carrying variables named by the configuration file](environment-variables.md#secret-carrying-variables-named-by-the-configuration-file) lists those settings.

---

## Settings

The table lists every setting, in the order of the schema, with its default and the page that explains it.
A setting whose member names are data, such as `dependencies.policies` and `dotnet.all.env`, is one row, and the linked page explains its members.

| Setting                               | Default                         | Meaning                                                                                                                                                            |
| ------------------------------------- | ------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `$schema`                             | none                            | The URL of the JSON schema, whose version segment [`bv self-update`](tool-commands/self-update.md#what-the-command-updates) moves with the other pins.             |
| `release.branches`                    | `[]`                            | The regular expressions of the branches that produce [public releases](sdk-modules/versioning.md#releasebranches-setting), and by default no branch does.          |
| `release.configuration`               | `dotnet.configuration`          | The [build configuration of `bv release`](tool-commands/release.md#the-build-configuration).                                                                       |
| `release.checkPublicApi`              | `true`                          | Whether the public API files take part in [the version spec change](tool-commands/release.md#the-version-spec-change) of `bv release`.                             |
| `release.changelogUpdates`            | `stable`                        | Which releases [update the changelog](tool-commands/release.md#the-changelog): `none`, `stable`, or `all`.                                                         |
| `release.emptyChangelog`              | none                            | The text of the [new changelog section](tool-commands/release.md#the-changelog) when "Unreleased changes" is empty, and without it such a release fails.           |
| `release.dogfood`                     | `true`                          | Whether `bv release` rewrites the self-references in [the post-release commit](tool-commands/release.md#the-post-release-commit).                                  |
| `versioning.prereleaseTag`            | none                            | The [prerelease tag](sdk-modules/versioning.md#versioningprereleasetag-setting) of a prerelease version, and without it no prerelease version is allowed.          |
| `versioning.assemblyVersionPrecision` | `major`                         | How many [version components](sdk-modules/versioning.md#versioningassemblyversionprecision-setting) the assembly version carries.                                  |
| `dotnet.configuration`                | `Release`                       | The [build configuration](tool-commands/build-pipeline.md#the-build-configuration) of the build pipeline.                                                          |
| `dotnet.all.args`                     | none                            | Arguments added to [every `dotnet` command](tool-commands/build-pipeline.md#the-pipeline) of the build pipeline, and to `dotnet nuget push`.                       |
| `dotnet.all.env`                      | none                            | Environment variables set on [every `dotnet` command](tool-commands/build-pipeline.md#the-pipeline) of the build pipeline, and on `dotnet nuget push`.             |
| `dotnet.restore.args`                 | none                            | Arguments added to [`dotnet restore`](tool-commands/build-pipeline.md#the-pipeline), after those of `dotnet.all`.                                                  |
| `dotnet.restore.env`                  | none                            | Environment variables set on [`dotnet restore`](tool-commands/build-pipeline.md#the-pipeline), over those of `dotnet.all`.                                         |
| `dotnet.build.args`                   | none                            | Arguments added to [`dotnet build`](tool-commands/build-pipeline.md#the-pipeline), after those of `dotnet.all`.                                                    |
| `dotnet.build.env`                    | none                            | Environment variables set on [`dotnet build`](tool-commands/build-pipeline.md#the-pipeline), over those of `dotnet.all`.                                           |
| `dotnet.test.args`                    | none                            | Arguments added to [`dotnet test`](tool-commands/build-pipeline.md#the-pipeline), after those of `dotnet.all`.                                                     |
| `dotnet.test.env`                     | none                            | Environment variables set on [`dotnet test`](tool-commands/build-pipeline.md#the-pipeline), over those of `dotnet.all`.                                            |
| `dotnet.pack.args`                    | none                            | Arguments added to [`dotnet pack`](tool-commands/build-pipeline.md#the-pipeline), after those of `dotnet.all`.                                                     |
| `dotnet.pack.env`                     | none                            | Environment variables set on [`dotnet pack`](tool-commands/build-pipeline.md#the-pipeline), over those of `dotnet.all`.                                            |
| `dotnet.nugetPush.args`               | none                            | Arguments added to [`dotnet nuget push`](tool-commands/release.md#publishing), after those of `dotnet.all`.                                                        |
| `dotnet.nugetPush.env`                | none                            | Environment variables set on [`dotnet nuget push`](tool-commands/release.md#publishing), over those of `dotnet.all`.                                               |
| `nuget.feeds.prerelease.source`       | `nuget.feeds.release.source`    | The URL of the feed that [prerelease versions](tool-commands/release.md#publishing) are pushed to.                                                                 |
| `nuget.feeds.prerelease.apiKeyEnv`    | `nuget.feeds.release.apiKeyEnv` | The environment variable holding the [API key](environment-variables.md#secret-carrying-variables-named-by-the-configuration-file) of the prerelease feed.         |
| `nuget.feeds.release.source`          | none                            | The URL of the feed that [stable versions](tool-commands/release.md#publishing) are pushed to.                                                                     |
| `nuget.feeds.release.apiKeyEnv`       | none                            | The environment variable holding the [API key](environment-variables.md#secret-carrying-variables-named-by-the-configuration-file) of the release feed.            |
| `github.tokenEnv`                     | `GITHUB_TOKEN`                  | The environment variable holding the [GitHub token](environment-variables.md#secret-carrying-variables-named-by-the-configuration-file) of the release operations. |
| `git.identity.name`                   | none                            | The name of the [author of the commits of `bv release`](tool-commands/release.md#preconditions).                                                                   |
| `git.identity.email`                  | none                            | The email address of the [author of the commits of `bv release`](tool-commands/release.md#preconditions).                                                          |
| `dependencies.scopes.netsdk`          | `major`                         | The [update policy](tool-commands/dependencies.md#update-policies) of the `netsdk` scope, the .NET SDK version in `global.json`.                                   |
| `dependencies.scopes.sdks`            | `minor`                         | The [update policy](tool-commands/dependencies.md#update-policies) of the `sdks` scope, the MSBuild project SDK pins.                                              |
| `dependencies.scopes.tools`           | `minor`                         | The [update policy](tool-commands/dependencies.md#update-policies) of the `tools` scope, the .NET local tool pins.                                                 |
| `dependencies.scopes.packages`        | `minor`                         | The [update policy](tool-commands/dependencies.md#update-policies) of the `packages` scope, the NuGet package pins.                                                |
| `dependencies.policies`               | none                            | The update policy of every pin whose id matches a pattern, where [the first match wins](tool-commands/dependencies.md#where-a-policy-comes-from).                  |
| `dependencies.additionalPackages`     | none                            | The [additional package groups](tool-commands/dependencies.md#additional-package-groups), keyed by caption, each with `files`, `items`, and an optional `policy`.  |
| `fileBasedApps`                       | `.buildvana/hooks/`             | Gitignore-syntax patterns naming the [file-based apps](tool-commands/dependencies.md#file-based-apps) of the repository, added to the hooks directory.             |

---

## Related files

- [`schemas/buildvana.schema.json`](../schemas/buildvana.schema.json) is the JSON schema, generated from the typed model.
  It names every setting, with its description, its built-in default value, and an example where one helps.
  An editor reads it to validate the file and to complete it.
- [`buildvana.example.jsonc`](../buildvana.example.jsonc) is a worked example, generated from that schema.
  Every setting appears, introduced by its description and carrying an example or default value.
  Nothing reads the file: copy out of it what you need.
- [`buildvana.jsonc`](../buildvana.jsonc) is this repository's own configuration, as the pinned version of `bv` reads it.
  It states what Buildvana sets for itself, and records in one line of prose each omission that is a decision.
- [`buildvana.next.jsonc`](../buildvana.next.jsonc) is the same configuration, as the _next_ version of `bv` will read it, so it may state a setting the pinned version rejects.
  The `release/post-release` hook copies it over `buildvana.jsonc`, in the same commit that moves the tool pin.

---

## Migration from the `.buildvana-home` marker

`.buildvana-home` no longer marks a home directory.
Delete the file, and create a `buildvana.jsonc` file holding `{}` in the same directory.
The new file marks the directory, as [Discovery](#discovery) says, and configures nothing until you add a setting.
