# Environment variables

This page lists every environment variable `bv` reads or sets. Variables consumed by the .NET SDK, MSBuild, or NuGet themselves are out of scope; see the corresponding Microsoft documentation.

## Variables read by `bv`

### `BV_DELEGATED`

The recursion guard for [delegation](DirectoryStructure.md#configdotnet-toolsjson): when a non-local `bv` hands an invocation over to the version pinned in the repository's tool manifest, it sets this variable on the delegated child, with the delegating `bv`'s version as the value. Its mere presence makes `bv` run in place unconditionally, so a delegated invocation can never delegate again, even if the installation layout is mis-detected.

The variable is not meant to be set by hand; to keep `bv` from delegating, pass `--skip-delegation` instead.

The marker is only true for the delegated `bv` itself, so `bv` removes the variable from the environment of its own child processes (solution builds, [hooks](Hooks.md), and so on): a `bv` reached through one of them — say, a globally-installed `bv` invoked by a hook — makes its own delegation decision, instead of inheriting a marker that is not about it. Hooks that need to know whether they run under delegation read the `RuntimeInfo.DelegatingVersion` member of their typed args.

### `CI_SERVER_HOST`

Set by GitLab CI to the hostname of the GitLab instance running the job. `bv` uses it to build the e-mail address of the CI bot identity (`gitlab-ci@noreply.<host>`), which authors the commits `bv release` creates when the repository's Git configuration names no committer of its own. Only meaningful together with `GITLAB_CI`, which is what makes `bv` use the GitLab adapter in the first place.

### `DOTNET_CLI_CONSOLE_USE_DEFAULT_ENCODING`

The .NET CLI's opt-out from having the console's encoding changed, honored by `bv` on the CLI's own terms so that a single variable governs the whole toolchain. Set it to `1` — the literal value, exactly as the CLI tests for it — and `bv` leaves the console's output and input encoding alone.

By default, `bv` sets both to UTF-8 for the duration of its run and restores the previous encodings on exit, as `dotnet` and MSBuild do, so that what `bv` can render depends neither on the codepage the console happened to be using nor on how `bv` was launched. Both encodings are set because that is what moves the console's active codepage: setting the output encoding alone takes effect in `cmd.exe` but not in PowerShell. The change is skipped where the console encoding APIs do not exist and, on Windows, below build 10.0.18363.

### `DOTNET_CLI_HOME`

Read the way the .NET CLI itself reads it: when set, it replaces the user profile directory as the root under which the CLI keeps its per-user state. `bv` consults it to locate the SDK's tool resolver cache (`.dotnet/toolResolverCache` under that root), which [delegation](DirectoryStructure.md#configdotnet-toolsjson) probes to decide whether the pinned `bv` is already installed or a `dotnet tool restore` must run first. When the variable is absent, the platform home directory applies (`USERPROFILE` on Windows, `HOME` elsewhere), as in the CLI.

### `DOTNET_HOST_PATH`

Set by the `dotnet` muxer on every process it launches, with the full path of the `dotnet` executable as the value. `bv` uses it to launch its child `dotnet` invocations (builds, tool restores, delegated runs, hooks) through the exact host that launched `bv` itself, instead of relying on `dotnet` being on the `PATH`. When the variable is absent — e.g. `bv` was installed as a global tool and run through its native shim — `bv` falls back to `dotnet` from the `PATH`.

### `GITHUB_ACTIONS`

Set to `true` by GitHub Actions on every step. `bv` reads it to recognize that it is running on GitHub Actions and act through the corresponding server adapter: releases go to the GitHub API, step outputs are published as described under `GITHUB_OUTPUT` below, and the `github-actions[bot]` identity authors the commits `bv release` creates when the repository's Git configuration names no committer of its own. The comparison is case-insensitive, so `TRUE` and `True` count as well; any other value, or no value at all, means "not GitHub Actions".

### `GITHUB_OUTPUT`

Set by GitHub Actions to the path of the file that collects a step's outputs. `bv release` appends to that file to publish the released version as the `version` step output, so that later steps of the same job can refer to it; the release fails if the variable is unset. `bv` never sets this variable itself.

### `GITLAB_CI`

Set by GitLab CI on every job. Its mere presence — whatever the value — makes `bv` recognize a GitLab CI run and act through the corresponding server adapter, including the bot identity built from `CI_SERVER_HOST` above.

### `NO_COLOR`

The widely-adopted convention for opting out of colored output; see [no-color.org](https://no-color.org). Any non-empty value — the convention's own rule, which counts presence rather than a particular value — turns off color in `bv`'s own narration. `--color` and `--no-color` win over it either way, so `bv --color` stays colored with `NO_COLOR` set.

Note that this rule deliberately differs from `DOTNET_CLI_CONSOLE_USE_DEFAULT_ENCODING`'s, which acts only on the literal value `1`. Each convention belongs to whoever defined it and is honored on its owner's terms; making the two agree would mean obeying neither.

### `TERM`

Read on non-Windows platforms only, where it is the POSIX way for a terminal to declare what it understands. An unset, empty, or `dumb` value tells `bv` that ANSI escape sequences would not be interpreted, so color auto-detection turns color off; `--color` still forces it on. On Windows the equivalent question is a console mode rather than a variable, and `TERM` is not consulted.

### Secret-carrying variables named by the configuration file

`bv` never stores secrets; the [configuration file](ConfigurationFiles.md) names the environment variable that carries each one, and `bv` reads the value at the point of use:

- `github.tokenEnv` names the variable holding the GitHub token used by release operations; the default name is `GITHUB_TOKEN`.
- `nuget.feeds.release.apiKeyEnv` and `nuget.feeds.prerelease.apiKeyEnv` name the variables holding the API keys for the NuGet push feeds.

## Variables set by `bv`

- `BV_DELEGATED` on the delegated `bv`, as described above — and removed from the environment of `bv`'s other child processes.
- The variables configured under `dotnet.all.env` and the per-command `dotnet.<command>.env` sections of the configuration file, on the corresponding child `dotnet` invocations; a `null` value removes the variable from the child's environment.
