# Environment variables

This page lists every environment variable `bv` reads or sets.
The variables that the .NET SDK, MSBuild, and NuGet read are out of scope, and their own documentation lists them.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Variables read by `bv`](#variables-read-by-bv)
  - [`BV_DELEGATED`](#bv_delegated)
  - [`CI_SERVER_HOST`](#ci_server_host)
  - [`DOTNET_CLI_CONSOLE_USE_DEFAULT_ENCODING`](#dotnet_cli_console_use_default_encoding)
  - [`DOTNET_CLI_HOME`](#dotnet_cli_home)
  - [`DOTNET_HOST_PATH`](#dotnet_host_path)
  - [`GITHUB_ACTIONS`](#github_actions)
  - [`GITHUB_OUTPUT`](#github_output)
  - [`GITLAB_CI`](#gitlab_ci)
  - [`NO_COLOR`](#no_color)
  - [`TERM`](#term)
  - [Secret-carrying variables named by the configuration file](#secret-carrying-variables-named-by-the-configuration-file)
- [Variables set by `bv`](#variables-set-by-bv)

---

## Variables read by `bv`

### `BV_DELEGATED`

The marker of a [delegated](command-line.md#delegation) run.
When `bv` hands an invocation over to the version the tool manifest pins, it sets the variable on the child.
The value is the version of the delegating `bv`.
A `bv` that finds the variable set runs in place, whatever else it detects, so a delegated invocation never delegates again.

Do not set the variable by hand.
To keep `bv` from delegating, pass `--skip-delegation`.

`bv` removes the variable from the environment of every child process it starts, such as a `dotnet` invocation or a [hook](hooks.md).
A `bv` that one of them starts then makes its own delegation decision.
A hook that needs to know whether the run was delegated reads the `RuntimeInfo.DelegatingVersion` member of its args.

### `CI_SERVER_HOST`

GitLab CI sets it to the host name of the GitLab instance that runs the job.
`bv` reads it to build the e-mail address of the CI bot identity, `gitlab-ci@noreply.<host>`.
That identity authors the commits of `bv release`, unless `buildvana.jsonc` states a `git.identity`.
The variable matters only together with `GITLAB_CI`, which is what selects the GitLab adapter.

### `DOTNET_CLI_CONSOLE_USE_DEFAULT_ENCODING`

The opt-out of the .NET CLI from changing the console encoding.
`bv` honors it under the rule of the CLI: the literal value `1` opts out, and any other value does not.

By default, `bv` sets the output and input encodings of the console to UTF-8 for its run, as `dotnet` and MSBuild do.
It restores the previous encodings on exit.
What `bv` can render then depends neither on the codepage of the console nor on how `bv` was launched.
Both encodings move because that is what changes the active codepage of the console.
Setting the output encoding alone takes effect in `cmd.exe` and not in PowerShell.
`bv` leaves the encodings alone where the console encoding APIs do not exist, and on Windows below build 10.0.18363.

### `DOTNET_CLI_HOME`

`bv` reads it as the .NET CLI does.
When the variable is set, it replaces the user profile directory as the root of the per-user state of the CLI.
`bv` uses it to locate the tool resolver cache of the SDK, `.dotnet/toolResolverCache` under that root.
[Delegation](command-line.md#delegation) probes the cache to decide whether the pinned `bv` is installed, or a `dotnet tool restore` must run first.
When the variable is unset, the platform home directory applies: `USERPROFILE` on Windows, `HOME` elsewhere.

### `DOTNET_HOST_PATH`

The `dotnet` muxer sets it on every process it starts, with the full path of the `dotnet` executable as the value.
`bv` starts its child `dotnet` processes through that path: builds, tool restores, delegated runs, and hooks.
The variable is unset when `bv` runs through the native shim of a global tool.
`bv` then runs the `dotnet` found on the `PATH`.

### `GITHUB_ACTIONS`

GitHub Actions sets it to `true` on every step.
`bv` reads it to recognize a GitHub Actions run and act through the GitHub adapter.
Releases then go to the GitHub API, and the step output goes to the file `GITHUB_OUTPUT` names.
The `github-actions[bot]` identity authors the commits of `bv release`, unless `buildvana.jsonc` states a `git.identity`.
The comparison ignores case, so `TRUE` and `True` count too.
Any other value, or no value, means no GitHub Actions.

### `GITHUB_OUTPUT`

GitHub Actions sets it to the path of the file that collects the outputs of a step.
`bv release` appends the released version to that file as the `version` step output, for the later steps of the job.
When the variable is unset, the release fails before creating anything.
`bv` never sets the variable.

### `GITLAB_CI`

GitLab CI sets it on every job.
Its presence, whatever the value, makes `bv` recognize a GitLab CI run and act through the GitLab adapter, with the bot identity built from `CI_SERVER_HOST`.

### `NO_COLOR`

The convention for opting out of colored output, documented at [no-color.org](https://no-color.org).
Any non-empty value turns off color in the narration of `bv`, as the convention prescribes.
`--color` and `--no-color` win over the variable, so `bv --color` stays colored with `NO_COLOR` set.

The rule differs from that of `DOTNET_CLI_CONSOLE_USE_DEFAULT_ENCODING`, which acts on the literal value `1` alone.
Each convention follows the rules of whoever defined it.

### `TERM`

`bv` reads it on every platform but Windows, where a terminal declares what it understands through this variable.
An unset, empty, or `dumb` value says that the terminal does not interpret ANSI escape sequences, so color detection turns color off.
`--color` still forces color on.
On Windows, a console mode answers the question, and `bv` does not read `TERM`.

### Secret-carrying variables named by the configuration file

`bv` stores no secret.
The [configuration file](configuration-file.md) names the environment variable that carries each one, and `bv` reads the value where it uses it.

- `github.tokenEnv` names the variable holding the GitHub token of the release operations.
  The default name is `GITHUB_TOKEN`.
- `nuget.feeds.release.apiKeyEnv` and `nuget.feeds.prerelease.apiKeyEnv` name the variables holding the API keys of the NuGet push feeds.

---

## Variables set by `bv`

- `BV_DELEGATED`, on the delegated `bv`, as described above.
  `bv` removes it from the environment of its other child processes.
- The variables under `dotnet.all.env` and under the per-command `dotnet.<command>.env` sections of the configuration file, on the matching child `dotnet` invocations.
  A `null` value removes the variable from the environment of the child.
