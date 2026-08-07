# Environment variables

This page lists every environment variable `bv` reads or sets. Variables consumed by the .NET SDK, MSBuild, or NuGet themselves are out of scope; see the corresponding Microsoft documentation.

## Variables read by `bv`

### `BV_DELEGATED`

The recursion guard for [delegation](DirectoryStructure.md#configdotnet-toolsjson): when a non-local `bv` hands an invocation over to the version pinned in the repository's tool manifest, it sets this variable on the delegated child, with the delegating `bv`'s version as the value. Its mere presence makes `bv` run in place unconditionally, so a delegated invocation can never delegate again, even if the installation layout is mis-detected.

The variable is not meant to be set by hand; to keep `bv` from delegating, pass `--skip-delegation` instead.

The marker is only true for the delegated `bv` itself, so `bv` removes the variable from the environment of its own child processes (solution builds, [release hooks](ReleaseHooks.md), and so on): a `bv` reached through one of them — say, a globally-installed `bv` invoked by a hook — makes its own delegation decision, instead of inheriting a marker that is not about it. Hooks that need to know whether they run under delegation read the `RuntimeInfo.DelegatingVersion` member of their typed context.

### `DOTNET_CLI_HOME`

Read the way the .NET CLI itself reads it: when set, it replaces the user profile directory as the root under which the CLI keeps its per-user state. `bv` consults it to locate the SDK's tool resolver cache (`.dotnet/toolResolverCache` under that root), which [delegation](DirectoryStructure.md#configdotnet-toolsjson) probes to decide whether the pinned `bv` is already installed or a `dotnet tool restore` must run first. When the variable is absent, the platform home directory applies (`USERPROFILE` on Windows, `HOME` elsewhere), as in the CLI.

### `DOTNET_HOST_PATH`

Set by the `dotnet` muxer on every process it launches, with the full path of the `dotnet` executable as the value. `bv` uses it to launch its child `dotnet` invocations (builds, tool restores, delegated runs, hooks) through the exact host that launched `bv` itself, instead of relying on `dotnet` being on the `PATH`. When the variable is absent — e.g. `bv` was installed as a global tool and run through its native shim — `bv` falls back to `dotnet` from the `PATH`.

### Secret-carrying variables named by the configuration file

`bv` never stores secrets; the [configuration file](ConfigurationFiles.md) names the environment variable that carries each one, and `bv` reads the value at the point of use:

- `github.tokenEnv` names the variable holding the GitHub token used by release operations; the default name is `GITHUB_TOKEN`.
- `nuget.feeds.release.apiKeyEnv` and `nuget.feeds.prerelease.apiKeyEnv` name the variables holding the API keys for the NuGet push feeds.

## Variables set by `bv`

- `BV_DELEGATED` on the delegated `bv`, as described above — and removed from the environment of `bv`'s other child processes.
- The variables configured under `dotnet.all.env` and the per-command `dotnet.<command>.env` sections of the configuration file, on the corresponding child `dotnet` invocations; a `null` value removes the variable from the child's environment.
