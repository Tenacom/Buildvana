# Info

`bv info` reports what `bv` detected about the machine and the repository, and what `bv release` would use.
This page says what each line of the report means, and what the command does when a line cannot be resolved.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [The report](#the-report)
- [Lines that cannot be resolved](#lines-that-cannot-be-resolved)
- [Exit codes](#exit-codes)

---

## The report

```text
bv info [OPTIONS]
```

The report has nine lines:

```text
CI platform:                GitHub Actions
Server URL:                 https://github.com/
Bot identity:               github-actions[bot] <41898282+github-actions[bot]@users.noreply.github.com>
ContinuousIntegrationBuild: true
Repository URL:             https://github.com/Tenacom/Buildvana
Release remote:             origin
Git host:                   GitHub (github.com)
Token variable:             GITHUB_TOKEN (set)
Release identity:           github-actions[bot] <41898282+github-actions[bot]@users.noreply.github.com>, from the bot identity of GitHub Actions
```

- The CI platform is the one `bv` detects from the environment, as [CI platforms](../introduction.md#ci-platforms) says.
- The server URL is the URL of the server that runs the CI platform, when the platform states one.
- The bot identity is the identity the CI platform commits with, when it has one.
- The `ContinuousIntegrationBuild` line holds the value the build pipeline passes to `dotnet`.
  `bv release` passes `true` on every platform, as [The steps](release.md#the-steps) says.
- The repository URL is the `repository.url` setting of `buildvana.jsonc`.
- The release remote is the remote of the clone that names that repository, as [The release remote](release.md#the-release-remote) says.
- The Git host line names the host of `repository.url`, and its host name.
  For an unknown host, the line says whether `release.allowUnknownHost` lets `bv release` proceed.
- The token variable is the environment variable that holds the token of the host, with whether it is set.
  `bv info` never prints the value of the token.
- The release identity is the identity the commits of `bv release` would have, with where it comes from.
  The source is the `git.identity` setting, the bot identity of the CI platform, or the Git configuration, as [Preconditions](release.md#preconditions) says.

The report is the result of the command, so it goes to standard output at every verbosity, and the narration goes to standard error.
The command needs no network, changes nothing, and runs no SDK version check.

---

## Lines that cannot be resolved

A line whose value does not exist reads `(none)`.
When the value cannot be resolved, the release remote line and the Git host line add the reason after `(none)`:

```text
Release remote:             (none) No remote of the clone names repository.url 'https://github.com/Tenacom/Buildvana'. The remotes of the clone are 'origin' (https://github.com/someone/Buildvana).
```

The reason is the message `bv release` would fail with.
Without `repository.url`, both lines state that the setting is missing, and the token variable line reads `(none)`.
Outside a Git repository, the release remote line states that there is no Git repository.
The release identity line then ignores the Git configuration.

---

## Exit codes

`bv info` returns the [exit codes every `bv` command returns](../tool-diagnostics.md#exit-codes).

Code 0 is a report, whatever its lines say.
Code 1 is an invalid `buildvana.jsonc`.
Code 2 is a refusal of the command line: an argument, an unknown option, or a `--` separator.
Code 130 is a run terminated with Ctrl-C.
