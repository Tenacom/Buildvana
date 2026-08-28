<!-- markdownlint-disable MD024 MD034 -->

# Changelog

All notable changes to Buildvana SDK will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Unreleased changes

**No stable 2.0 release: here's why.**  
When we switched from Nerdbank.GitVersioning to our own versioning code, `version.json` gave way to `VERSION` and the patch number restarted from 1 — far below the 2.0.x versions we had already published. Releasing another 2.0.x was thus impossible (per SemVer, enforced by `bv release`) short of 200+ more commits, so we bumped the minor version instead: hence 2.1-preview.  
See the Nerdbank.GitVersioning removal entry under _Changes to existing features_ for how to correctly handle the patch-number restart when migrating your own repository.

### New features

- `bv` now prints a startup logo (`Buildvana CLI tool v{version}`) before running the requested command. Pass `--nologo` to suppress it.
- `bv --version` prints the tool's informational version and exits without running a command and without printing the startup logo.
- `bv` root help (`bv --help`) now shows a `GLOBAL OPTIONS:` section listing the options every subcommand inherits (`--verbosity`/`-v`, `--color`, `--no-color`, `--nologo`, `--version`). These options are now position-independent (accepted before or after the subcommand name) and case-insensitive, matching the rest of `bv`'s option surface.
- Commands that forward extra arguments to `dotnet` (`restore`, `build`, `test`, `pack`) are marked as such in `bv`'s root help, and their per-command help (`bv <command> --help`) includes a `FORWARDED ARGUMENTS` section.
- Buildvana now recognizes a repository-root configuration file, `buildvana.json` (or its commented variant `buildvana.jsonc`). It is discovered, parsed, validated, and exposed to `bv`; the settings it currently drives are listed below, and more are wired in over subsequent releases. A committed JSON schema (`schemas/buildvana.schema.json`) is generated from the typed model, so editors can validate and document the file, each setting's built-in default value included; unknown keys, an invalid file, or the presence of both `buildvana.json` and `buildvana.jsonc` in the same directory are reported as errors and will prevent `bv` from executing _any_ subcommand, even those that are not driven by any configuration setting (e.g., `clean`).
- Both `bv` and the SDK now treat a `buildvana.json`/`buildvana.jsonc` file as a home-directory marker, alongside Git markers; home-directory discovery now stops at the nearest directory (the starting directory included) that contains any marker.
- `buildvana.json` now drives several build and release settings that were previously CLI-only or hardcoded. Each resolves as CLI flag (where one exists) → `buildvana.json` → built-in default. A blank or all-whitespace string is never a value, wherever it is stated: a blank value for a CLI option is rejected like a missing one, a blank optional setting counts as not stated (the next tier applies), and a blank required member fails validation at configuration load. The settings:
  - the default build configuration (`dotnet.configuration`, default `Release`), used by `bv restore`/`build`/`test`/`pack` and as the base of `bv release`'s configuration chain (`--configuration` → `release.configuration` → `dotnet.configuration`);
  - extra arguments and environment variables for each `dotnet` invocation: `dotnet.all` (applied to every invocation) merged with the per-command `dotnet.restore`/`dotnet.build`/`dotnet.test`/`dotnet.pack`/`dotnet.nugetPush`, each carrying `args` and `env`. Arguments are appended in the order base → `dotnet.all` → per-command → forwarded command-line arguments (so a `--` argument still wins); environment variables apply `dotnet.all` then the per-command entries;
  - the `bv release` settings `release.checkPublicApi`, `release.dogfood`, and `release.changelogUpdates` + `release.emptyChangelog`.
- `buildvana.json` now also holds the secrets and endpoints `bv release` needs when publishing, replacing the fixed environment variables read previously. Secrets are never inlined: configuration names the environment variable that carries each one, and the value is read at the point of use.
  - NuGet push feeds (`nuget.feeds`): a `release` channel and an optional `prerelease` channel, each `{ source, apiKeyEnv }`, with both members required, and required to be non-blank, whenever a feed is stated — a half-written feed fails at configuration load rather than at push time. `bv release` pushes prerelease versions to the `prerelease` feed — falling back to the `release` feed when `prerelease` is omitted — and stable versions to the `release` feed. The feed URL comes from `source`; the API key is read from the environment variable named by `apiKeyEnv`. Feed selection no longer depends on whether the repository is private: the old `private` channel is gone, although `bv` can still query a repository's visibility.
  - the GitHub token (`github.tokenEnv`, default `GITHUB_TOKEN`): names the environment variable that holds the token used for release operations.
- `buildvana.json` accepts a `git.identity` (`{ name, email }`, both members required and non-blank whenever the section is stated) section describing the author/committer for automated commits. `bv release` resolves the identity for its commits as `git.identity` → the CI bot identity supplied by the server adapter → the repository's own Git configuration, and fails before building anything when none of the three exists.
- The `ThisAssemblyClass` SDK module, removed along with code generation tasks after v1.0.0-alpha.20, has been reintroduced. Setting the `GenerateThisAssemblyClass` property to `true` (default: `false`) in a C# project generates a `ThisAssembly` static class containing constants defined via `ThisAssemblyConstant` items, using the syntax documented in [docs/ConstantsSyntax.md](docs/ConstantsSyntax.md). A set of default constants (assembly version, company, product, etc.) is defined unless the `EnableDefaultThisAssemblyConstants` property is set to `false`; the class name and namespace can be customized via the `ThisAssemblyClassName` and `ThisAssemblyClassNamespace` properties. Unlike its previous incarnation, the feature is implemented as a Roslyn incremental source generator, and supports C# projects only: setting `GenerateThisAssemblyClass` to `true` in a project in any other language raises warning BVSDK2300.
- Buildvana now computes project versions natively, replacing Nerdbank.GitVersioning end-to-end (see the corresponding breaking change below). The single source of truth for the version is a plain-text `VERSION` file at the repository root, holding a `MAJOR.MINOR[-[tag]]` specification; the patch number is the Git height of the version line, i.e. the number of commits since the last change of `MAJOR.MINOR`, computed with the same rules as Nerdbank.GitVersioning (counting from 1 at the bump commit, longest path across merges; prerelease-only edits to `VERSION` do not reset the height). Versioning policy lives in `buildvana.json`:
  - `release.branches`: regular expressions (matched against the short branch name) identifying branches that produce public releases. On other branches, and in detached-HEAD state, the build is a non-public release, and the informational version carries a short commit ID (`1.2.3-preview.g0123456789` on prereleases, `1.2.3-g0123456789` on stable versions);
  - `versioning.prereleaseTag`: the effective prerelease tag (e.g. `preview`), required when `VERSION` marks a prerelease line (the tag text in `VERSION` itself is informational only);
  - `versioning.assemblyVersionPrecision` (`major` | `minor` | `build`, default `major`): how much of the computed version goes into `AssemblyVersion`.

  With a `VERSION` file present, every project built with Buildvana SDK gets `$(Version)`, `$(PackageVersion)`, `$(AssemblyVersion)` (precision-controlled), `$(FileVersion)` (`MAJOR.MINOR.HEIGHT.0`), and `$(InformationalVersion)` computed at build time by a compiled task using LibGit2Sharp — no `git` executable and no external package required — under plain `dotnet build` as well as `MSBuild.exe` and `bv`. The `UseVersioning` property overrides the automatic module activation in both directions. `bv release` now computes versions the same way, in-process.
- Versioned C# projects get the generated `ThisAssembly` class by default: when the `Versioning` module is active, `GenerateThisAssemblyClass` defaults to `true`, and the module contributes versioning constants alongside the default assembly-attribute constants: `SimpleVersion`, `SemVer`, `IsPublicRelease`, `IsPrerelease`, and `GitCommitId`. Compared with Nerdbank.GitVersioning's `ThisAssembly`, the `GitCommitDate`/`GitCommitAuthorDate` and `PublicKey`/`PublicKeyToken` constants are not provided, while `AssemblyDescription`, `SimpleVersion`, and `SemVer` are new.
- `bv` now runs optional repository-owned hooks: [file-based apps](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/program-structure/file-based-programs) at well-known paths of the form `.buildvana/hooks/<context>/<event>.cs` — `<context>` names the context the event belongs to (currently always the invoking command), `<event>` the moment of execution that triggers the hook — run via `dotnet run` from the home directory when the event occurs, and skipped with an info message when absent. Args are passed through a JSON file at a per-hook well-known path, `.buildvana-temp/hook-args/<context>/<event>.json`, (re)written before each hook run and left in place afterwards, so a hook can be re-run by hand against the args of the last run; `.buildvana-temp/` is bv's scratch directory for machine-generated temporary files, recommended for gitignoring and always excluded from bv's own working-tree change detection. `bv clean` clears the hooks' file-based-app build caches and deletes the scratch directory. The first event is `release/post-release`, raised by `bv release` at the moment the post-release commit is assembled: before the built-in self-reference rewrites (when dogfooding is enabled) and before anything is pushed — the hook is a file-based app inside the repository tree, so it builds against the version pins as they stand before the release and reads the version being released from its args. The hook runs whether or not dogfooding is enabled; files it changes join the post-release commit alongside the built-in rewrites, and a non-zero exit code aborts the release before anything is pushed. See [docs/Hooks.md](docs/Hooks.md) for the full contract.
- A new packaged library, `Buildvana.Runtime`, holds the typed models `bv` shares with repository-owned hooks: the resolved Buildvana configuration and the run-time information of a run. `BuildvanaConfig` is the _resolved_ configuration — every setting at its effective value, with the configuration file, the command line, and the built-in defaults already composed, so every consumer reads a setting by property access instead of spelling out its own fallback. Every default lives on the model itself, as a property initializer, and a member is nullable only when `null` has exactly one domain meaning (e.g. `versioning.prereleaseTag`: prereleases are not allowed). Credentials are stored as environment-variable names, never as values; extension methods such as `GetToken()` on the `github` section resolve the named variable on demand, and — being extension methods rather than members — are never serialized. The library also holds `PostReleaseHookArgs` (with `PostReleaseHookArgs.Load()`, structured into a `RuntimeInfo` section — shared by the args of every hook through the `HookArgs` base record, and carrying the running `bv`'s version, the delegating `bv`'s version when the run was delegated, the absolute paths of the run's well-known directories, the absolute path of the configuration file the run read (so that a hook working on that file acts on the one `bv` read instead of searching for it), and the run's resolved configuration itself, snapshotted into the args when they are written: a hook reads the effective settings of the very run its args belong to, not whatever the configuration file happens to say by the time the hook runs — plus a `Release` section and other hook-specific members; every args type also implements the `IHookEvent` interface, naming its hook's context and event as static properties, so that `bv` dispatches a hook from its args type alone and a hook's identity can never be mismatched with its args), and the well-known paths shared by both sides of the hook contract (directory constants and per-hook path helpers). A hook references the package with an unversioned `#:package Buildvana.Runtime` directive; Buildvana SDK pins the package to its own version for every file-based app built in the repository, so `bv`, the SDK, and hooks always agree on the shape of the data.
- Before running any command that uses Buildvana SDK (`restore`, `build`, `test`, `pack`, and `release`), `bv` now verifies that the repository pins the SDK (the `Buildvana.Sdk` entry under `msbuild-sdks` in `global.json`) at its own version: `bv`, `Buildvana.Sdk`, and `Buildvana.Runtime` are released in lockstep, and a version mismatch — a half-updated repository, a newer globally-installed tool against an older pin — would otherwise produce silent behavior drift. A missing `global.json`, section, or entry counts as a mismatch. On mismatch, the command fails with a message naming both versions and the ways to align them. Versions are compared by SemVer precedence, ignoring build metadata. The new global option `--skip-sdk-check` skips the check, for scenarios that require a deliberate mismatch (e.g. bisecting an SDK regression in CI).
- `bv` now delegates to the repository's pinned version: whenever the tool manifest (`.config/dotnet-tools.json`) pins `bv`, the pinned version is the one that runs, no matter which `bv` is invoked — like the Angular CLI, where the global `ng` always hands over to the project-local install. The invoked `bv` makes sure the pinned version is installed — probing the SDK's tool resolver cache the same way `dotnet tool run` does, and running `dotnet tool restore` only when needed; a failed restore is reported but does not block the attempt — and hands it the entire original command line (`dotnet tool run bv`) with inherited standard streams, forwarding its exit code. The delegated `bv` runs from the home directory, so a relative path inside forwarded arguments resolves against the home directory rather than the invocation directory; and `--version` answers for the pinned `bv` (pass `--skip-delegation` to ask the invoked binary). When the versions differ, an info line on standard error names the version that runs. A delegating `bv` does not judge the command line beyond the minimal split that finds the subcommand and the global options (only a value-bearing global option with no following value, such as a trailing `-v`, is rejected before delegation, with the same message in every version), and does not read the configuration file — both may be valid for the pinned version and not for the invoked one, and judging them is the pinned version's job. The new `self-update` subcommand is exempt (see below); the new global option `--skip-delegation` runs the exact binary invoked; and the `BV_DELEGATED` environment variable, set on the delegated child, guarantees that a delegated invocation never delegates again. The variable is removed from the environment of every other child process `bv` spawns, so a `bv` reached through a hook or a build makes its own delegation decision. A `bv` invoked outside a repository, or in a repository whose tool manifest does not pin `bv` (the entry is matched case-insensitively, like the dotnet CLI matches it; an entry with an unusable version is reported and treated as no pin), runs in place as before.
- A new `bv self-update` command updates the repository's entire Buildvana surface to one version in one operation: by default the running `bv`'s own version, or the one named by `--to <version>`. (Canonically, "self-update" means "replace my own binary"; this one stamps a version into the repository's pins and never touches the binary.) It updates: the `bv` pin in the tool manifest, via `dotnet tool update` (or `dotnet tool install --create-manifest-if-needed` when there is no entry yet), which also downloads the version — with `--to`, that step doubles as the existence check, so a version no configured source knows fails the update before any file is written; the `Buildvana.Sdk` pin in `global.json`, creating the file and/or the `msbuild-sdks` section if needed and preserving the file's formatting otherwise; every pin of a Buildvana family package — `bv`, `Buildvana.Sdk`, `Buildvana.Runtime`, a deliberately closed list, so a third-party `Buildvana.*` package is never dragged along — declared in the repository's own files, i.e. `PackageVersion`/`GlobalPackageReference`/`PackageReference` items — plus the item name of every group declared under the `dependencies.additionalPackages` configuration setting — in projects and shared props/targets files, and versioned `#:package`/`#:sdk` directives in file-based apps, found by a gitignore-aware walk (build debris never contributes a pin; `.cs` files are read only within the file-based-app scope, i.e. `.buildvana/hooks/` plus the gitignore-syntax patterns of the new `fileBasedApps` configuration setting, so discovery cost does not scale with the size of the source tree) and spliced in place byte-preservingly, with a pin whose version is not a literal (a property reference, a range, a floating version) reported and left alone; and the version segment of the configuration file's `$schema` reference, when it points at the canonical `Tenacom/Buildvana/<version>/schemas/` URL. The summary lists every family pin found, one line per pin naming its declaring file — unchanged and left-alone pins included — so it doubles as a check that every intended pin was discovered. Afterwards, the configuration file is loaded with the new version's model, and any problems are reported as warnings for review. `self-update` is exempt from delegation — it updates the repository to the `bv` actually invoked ("bring this repository to me"), so the usual upgrade flow is `dotnet tool update -g bv` followed by `bv self-update`, and `dnx bv@<version> self-update` targets any specific version — and it refuses to downgrade a repository whose pins are newer than the target version, unless `--force` is passed. A manifest whose `bv` entry pins an invalid version is beyond the dotnet CLI's reach entirely (the CLI cannot parse such a manifest), so `bv self-update` fails up front with a message naming the entry to fix.
- `bv` has a new `version` command group for working with native versioning outside of a release. `bv version show` (also reachable as plain `bv version`) prints the computed current version alongside the latest and latest stable published versions, the public-release and prerelease flags, and the current branch; the report is the command's deliverable, printed regardless of verbosity and alone on standard output, so it stays pipeable whatever the verbosity (diagnostics go to standard error; pass `--verbosity normal` or higher for more of them). `bv version advance [CHANGE]` applies a version-spec change (`none`, `unstable`, `stable`, `minor`, or `major`, the same values as `bv release --bump`) to the `VERSION` file, running the change through the same analysis as `bv release` (published-version comparison plus public API check, the latter controlled by `--check-public-api` and `release.checkPublicApi`); pass `--force` to apply the requested change verbatim, skipping the analysis. The modified `VERSION` file is left uncommitted for review.

### Changes to existing features

- **BREAKING CHANGE**: In projects that use InnoSetup to produce installers, the `Tools.InnoDownloadPlugin` package is not automatically added as a dependency any longer. InnoSetup Download Plugin is an unmaintained 32-bit DLL: unusable in Inno Setup 7's 64-bit installers. Use the built-in [`download`/`issigverify` flags](https://jrsoftware.org/ishelp/topic_filessection.htm) instead.
- **BREAKING CHANGE**: `bv` now defaults to `minimal` verbosity, for every command alike; it used to default to `normal`. The build pipeline commands wrap `dotnet restore`/`build`/`test`/`pack`, which default to `minimal` themselves and receive `bv`'s verbosity verbatim, so `bv build` used to produce a markedly noisier MSBuild log than plain `dotnet build`; it now produces a comparable one. Nothing fails and no migration is required: pass `-v normal` for the previous output. Note that `bv`'s own narration — activity start/finish lines and the `info:` lines describing what the tool is doing — is hidden at the new default, while the record of what a command _did_ survives it (see the new `notice:` level below), so `bv release` still logs its complete account of what it changed and published.
- A new message level, rendered as `notice:`, sits between `warning:` and `info:` and is shown from `minimal` verbosity up. It carries the messages that record a fact — something changed, something was decided, something was deliberately skipped — as opposed to the narration of what a command is doing at a given moment, which stays at `info:`. Buildvana SDK tasks and `bv` now agree on the verbosity at which each level becomes visible: previously a message logged by a task showed up one rung earlier than the same message printed by `bv`.
- **BREAKING CHANGE**: The `.buildvana-home` marker file is no longer recognized: home-directory discovery now only looks for a Buildvana configuration file (in the home directory itself) and Git markers. `.buildvana-home` predates the configuration file, and a `buildvana.json` containing an empty object (`{}`) does the same job — marking a directory as home without configuring anything — while being what one would naturally reach for. To migrate, replace `.buildvana-home` with a `buildvana.json` file containing `{}`.
- **BREAKING CHANGE**: The `JetBrainsAnnotations` module no longer adds any JetBrains annotations package to your project, and the `UseJetBrainsAnnotations` property has been removed. To export ReSharper external annotations, reference an annotations source yourself — the compiled `JetBrains.Annotations` package, `JetBrains.Annotations.Sources`, or your own attributes in the `JetBrains.Annotations` namespace — and set the new boolean `ExportJetBrainsAnnotations` property (default `false`) to `true`. When enabled, Buildvana SDK reads the annotations from source with Roslyn after each build and packs a `{AssemblyName}.ExternalAnnotations.xml` file next to the assembly in `lib/<tfm>`, one per target framework. The export no longer depends on Mono.Cecil or on a second build pass; and, when the annotation attributes are `[Conditional("JETBRAINS_ANNOTATIONS")]` (as in the JetBrains packages), no JetBrains attribute metadata remains in the compiled assembly, leaving clean IL and AOT output.
- **BREAKING CHANGE**: The `JetBrainsAnnotations` module no longer supports Visual Basic projects. `ExportJetBrainsAnnotations` is forced to `false` for any project that is not a C# (`.csproj`) project, because the exporter reads C# source directly.
- `bv` may now be invoked from any subdirectory under the solution's directory (`HomeDirectory` property in Buildvana SDK). It will search upwards for the home directory, the same way the SDK does, and work from there. If it doesn't find the home directory, `bv` will exit with a non-zero exit code. As soon as the home directory is discovered, `bv` makes it the process's current directory: from that point on, every relative path — including relative paths in forwarded arguments — resolves against the home directory no matter where `bv` was invoked from, matching delegated runs, which are spawned from the home directory. Commands that never need the home directory (e.g. `bv --version`) leave the current directory untouched.
- **BREAKING CHANGE**: The `prepare` subcommand of `bv` has been renamed to `clean`, in order to maintain parallelism between `bv` and `dotnet` build pipeline subcommands (`restore`, `build`, etc.).
- **BREAKING CHANGE**: Microsoft Testing Platform (MTP) is the only supported test runner now. Test projects using VSTest are explicitly not supported and will cause test-time errors. Buildvana will assume all test projects to be built with MTP.
- **BREAKING CHANGE**: Code coverage reports are now collected in a `TestResults` directory at the repository root rather than under each test project's directory, and are no longer merged into a single file. Tooling that consumes coverage output (e.g. Codecov upload steps) must handle multiple *.cobertura.xml files; Codecov's GitHub Action accepts globs natively.
- **BREAKING CHANGE**: Test projects must set the `IsTestingPlatformApplication` MSBuild property to `true`.  In practice, all major MMTP-compatible test frameworks already do this through their SDK, so explicit assignment in project files is rarely needed. The old heuristic rule by which projects whose name ends in `.Tests` were considered test projects by default is no longer in effect. The `IsTestProject` property, used to identify VSTest test projects, is not checked.
- **BREAKING CHANGE**: `bv clean` (formerly known as `bv prepare`) now deletes the `TestResults` directory at the repository root.
- `bv clean` (formerly known as `bv prepare`) no longer deletes per-project `TestResults` directories.
- `dotnet bv release` no longer folds self-reference (dogfood) updates into the "Prepare release" commit. They now go into a separate `Update self-references to <version> [skip ci]` commit pushed on top, in the same push. The release tag binds to the "Prepare release" commit, so checking out the tag and rebuilding now reproduces the actually-released source state (which still references the previously-published versions). `[skip ci]` is required on the dogfood commit because the new packages are usually not yet published at push time.
- `bv release` now prefers the CI bot identity supplied by the server adapter over whatever identity the repository's Git configuration happens to contain, so release commits are attributed deterministically rather than to whatever a previous CI step left behind. Previously the repository's configuration won. A `git.identity` section in `buildvana.json` outranks both (see _New features_ above).
- **BREAKING CHANGE**: Three `bv release` options have been renamed:
  - `--versionSpecChange` → `--bump`
  - `--checkPublicApiFiles` → `--check-public-api`
  - `--updateSelfReferences` → `--dogfood`
- **BREAKING CHANGE**: `bv` no longer accepts CLI option values via environment variables. The following env vars are no longer recognized as defaults for their CLI counterparts:
  - `CONFIGURATION` (`--configuration`)
  - `VERSION_SPEC_CHANGE` (`--versionSpecChange`, now `--bump`)
  - `CHECK_PUBLIC_API_FILES` (`--checkPublicApiFiles`, now `--check-public-api`)
  - `UPDATE_SELF_REFERENCES` (`--updateSelfReferences`, now `--dogfood`)

  Pass values via CLI flags instead.  
  Secrets and endpoints are no longer read from fixed environment variables (`GITHUB_TOKEN`, `PRIVATE_NUGET_SOURCE`/`KEY`, `PRERELEASE_NUGET_SOURCE`/`KEY`, `RELEASE_NUGET_SOURCE`/`KEY`) either; they are now configured in `buildvana.json` (`nuget.feeds`, `github.tokenEnv`) as described under _New features_ above.
- **BREAKING CHANGE**: `bv`'s `--verbosity` setting now accepts the same values as the .NET CLI:
  - `quiet` (or `q`)
  - `minimal` (or `m`)
  - `normal` (or `n`)
  - `detailed` (or `d`)
  - `diagnostic` (or `diag`)

  Cake verbosity values (e.g., `verbose`) are no longer accepted.
- `bv` no longer prefixes its console output with a log level and a class-name category (e.g. `info: Buildvana.Tool.Services.DotNetService: ...`). Messages now render as clean, color-coded lines: errors in red and warnings in yellow, each line tagged with a short level label (`error:`/`warning:`/`info:`/`detail:`/`trace:`). In addition, `dotnet`/MSBuild output is now streamed through live (standard output to `bv`'s standard output, standard error to its standard error) instead of being hidden unless the build fails; on failure, the first and last lines of the captured output are still included in the error message. Verbosity behavior is unchanged (`--verbosity quiet|minimal|normal|detailed|diagnostic`), as is the handling of `--color`/`--no-color` (with the [`NO_COLOR` environment variable](https://no-color.org) now honored as well).
- **BREAKING CHANGE**: `bv` now writes all of its own narration to standard error, keeping standard output for actual results, per the prevailing CLI convention (git, npm, cargo, etc.). Narration comprises the leveled diagnostic lines (`error:`/`warning:`/`info:`/`detail:`/`trace:`), activity start/finish lines, and the startup logo. Standard output now carries only command deliverables (e.g. `bv version show`'s report) and the standard output of child `dotnet` processes, which is the payload of the build commands. Results thus stay pipeable at any verbosity: `bv version show | some-parser` receives only the report, and `bv build 2>bv.log` separates `bv`'s narration from `dotnet`'s output. Scripts and CI steps that captured diagnostics from `bv`'s standard output must now capture standard error instead (e.g. via `2>&1`). Color auto-detection consequently probes standard error: a redirected standard error disables color, while a redirected standard output no longer does.
- **BREAKING CHANGE**: `bv restore`, `bv build`, `bv test`, and `bv pack` forward extra command-line arguments to the underlying `dotnet` invocation(s) only after a `--` separator: everything after the first `--` is passed through verbatim, in the order given, and `bv` no longer parses or validates it — with one exception, `-c`/`--configuration`, which belongs to `bv` even there (see the dedicated entry below). A non-global, option-looking token _before_ `--` is now an error that points you at the separator. Malformed or unknown forwarded arguments produce an error from `dotnet` (or, for `bv test`, from the Microsoft.Testing.Platform test application) rather than from `bv`. Previously only `-p:`/`/p:` MSBuild properties were forwarded. `bv` also always forwards `--nologo` and its resolved `--verbosity` (default `minimal`) to those invocations.
  - `bv build -- -m:8 -v:minimal` forwards `-m:8 -v:minimal` to `dotnet build`.
  - `bv test -- --report-trx` reaches the test application.
- **BREAKING CHANGE**: `bv release` rejects a `--` separator (and anything after it): unlike the pipeline commands, it has no underlying `dotnet` pass-through to forward arguments to.
- **BREAKING CHANGE**: `bv` no longer forces `-maxcpucount:1` on the `dotnet` invocations of `restore`/`build`/`test`/`pack`. MSBuild now uses its default parallelism unless you forward your own `-m`/`-maxcpucount` switch.
- **BREAKING CHANGE**: The `-c`/`--configuration` option is no longer parsed by `bv restore`/`build`/`test`/`pack` in front of the `--` separator; it moves after it, where it is the one option `bv` still recognizes as its own. A `-c Debug` (or `--configuration=Debug`) stated among the forwarded arguments drives `bv`'s own view of the build configuration — `bv pack -- -c Debug` builds, reports, and locates artifacts for `Debug` — and is stripped from what reaches the `dotnet` commands: `bv` injects the resolved configuration itself, in the form each command accepts (`dotnet test` takes `--property:Configuration=`, the other commands take `-p:Configuration=`, and `dotnet restore`, which rejects the option outright, gets none). Owning these two names in the forwarded stream has two consequences: a trailing `-c`/`--configuration` with no value after it is an error from `bv`, and a `-c` that was meant as the _value_ of another forwarded option is captured as `bv`'s configuration. `bv release` keeps `-c`/`--configuration` as a parsed option, since it needs the value to locate build artifacts.
- **BREAKING CHANGE**: The `--main-branch` global option has been removed, along with `bv`'s main-branch discovery. The human-curated changelog permalink in generated release notes now points at the release branch itself rather than the discovered main branch.
- **BREAKING CHANGE**: The `--unstable-changelog` and `--require-changelog` options of `bv release` have been removed with no CLI replacement; changelog policy is repository-stable, not per-invocation. Configure it in `buildvana.json` instead: `release.changelogUpdates` (`none` | `stable` | `all`, default `stable`) selects which releases update the changelog, and `release.emptyChangelog` provides substitute text for an empty "Unreleased changes" section (when unset or blank, an empty section fails the release, matching the previous `--require-changelog` default of `true`).
- `bv`'s build commands (`clean`, `restore`, `build`, `test`, `pack`) and `release` now observe cancellation. Pressing Ctrl-C (or a host cancelling the operation) stops the pipeline promptly: it stops launching further steps and terminates the running `dotnet` child process instead of waiting for it to finish, then `bv` exits with code 130. Partial build output may be left behind on cancellation; `bv clean` recovers.
- **BREAKING CHANGE**: Nerdbank.GitVersioning has been removed from Buildvana SDK and `bv`. Versions are now computed natively from a `VERSION` file and `buildvana.json` keys (see _New features_ above): the `NerdbankGitVersioning` SDK module is gone, the `Nerdbank.GitVersioning` package is no longer injected into projects, `version.json` is no longer read, and `bv` no longer invokes the `nbgv` CLI. The `GetBuildVersion` target name is retained (as a real target or a stub) for targets that depend on it. To migrate a repository:
  - create a `VERSION` file at the repository root holding the `version` value from `version.json` (e.g. `2.0-preview`). **The Git height restarts at the commit that creates `VERSION`**: it counts commits since `MAJOR.MINOR` last changed in that file, and a file that did not exist before counts as a change, so the history of the `version.json` it replaces does not carry over. If your latest published patch number is high, bump `MAJOR.MINOR` in the same commit: on a fresh version line the restart is harmless, whereas keeping the old line computes versions lower than the ones you already published;
  - move `publicReleaseRefSpec` to `release.branches` in `buildvana.json`, converting refspec patterns to short branch-name regular expressions (e.g. `^refs/heads/main$` → `^main$`);
  - move `release.firstUnstableTag` to `versioning.prereleaseTag`, and `assemblyVersion.precision` to `versioning.assemblyVersionPrecision`;
  - delete `version.json` and remove `nbgv` from your tool manifest, if present.
- **BREAKING CHANGE**: The `UseNerdbankGitVersioning` property has been renamed to `UseVersioning`. The old name still works as an alias (when `UseVersioning` itself is not set), but raises warning BVSDK2001 and will be removed in a future version.
- **BREAKING CHANGE**: `version.json` features without a native equivalent are dropped: `pathFilters` is not supported (the version height is always computed from the whole history of the version line), nested `version.json` files are not supported (`VERSION` lives at the repository root only), and the NuGet package version scheme is fixed at SemVer 2.0.
- `bv` now sets the console's output and input encoding to UTF-8 for the duration of its run, restoring the previous encodings when it exits, exactly as the .NET CLI and MSBuild already do. What `bv` could render previously depended on how it was launched: invoked through the muxer (`dotnet bv`), the CLI had already switched the console to UTF-8 before `bv` started, whereas a globally-installed `bv` run through its native shim used whatever codepage the console happened to have — one where characters outside it are not rejected but silently best-fit mapped to lookalikes. Setting `DOTNET_CLI_CONSOLE_USE_DEFAULT_ENCODING` to `1` — the .NET CLI's own opt-out, honored by `bv` so that a single variable governs the whole toolchain — leaves the console untouched. The switch is skipped on platforms without console encoding APIs and, on Windows, below build 10.0.18363, matching the CLI's own guards. Buildvana SDK is unaffected: its tasks run inside MSBuild, which already does this.

### Bugs fixed in this release

- Buildvana SDK now correctly checks the `IsTestingPlatformApplication` (required by MTP) instead of `IsTestProject` (required by VSTest) to determine whether a project is a test project and set `BV_IsTestProject` accordingly.
- `bv release` no longer tags and publishes a version one patch above the one its artifacts were built with. The "Prepare release" commit bumps the Git height, hence the version, but it was only created when an earlier step had a file to commit; a release with nothing to commit before the build (typically a prerelease with no version-spec change and `release.changelogUpdates` set to `stable` or `none`) therefore built and pushed its packages at the pre-commit version, then created the commit, and tagged and released the version above. The release commit is now always created before the build.
- `bv release` no longer tags and publishes a version one patch _below_ the one its artifacts were built with when the release applies a version-spec change (`--bump minor` or `--bump major`, or a minor bump forced by an additive public API change). The `VERSION` file reached the "Prepare release" commit only after the version had been computed, so at computation time the new version line had no commit carrying it, and its Git height came out as 0 — hence versions such as `2.4.0-preview`, which the height calculation can never legitimately produce, since it counts from 1 at the bump commit and reserves 0 for a version line with no committed history. The artifacts, built after the commit, correctly carried `2.4.1-preview`, while the tag, the release, and the hook args said `2.4.0-preview`; produced-package discovery, which matches packages by version, therefore found none, and the self-reference (dogfood) updates were silently skipped. Files are now staged before the release commit is created, and the version is refreshed after every change to the commit's contents, so what is tagged and published is always what a build of the tagged commit produces.
- `bv release` now refuses to publish a version whose Git height is 0, i.e. one whose version line is carried by no commit in the branch's history — typically because `VERSION` has never been committed. Such a version cannot be reproduced: a build of the tagged commit would count the height from 1 as soon as the file reached it, and produce a different version. Building in that state remains perfectly legitimate — it is what a working tree looks like between `bv version advance` and the commit of its result — so only releasing is refused, with a message naming the file to commit.
- `bv release` no longer fails _after_ publishing a release when `GITHUB_OUTPUT` is not set. The variable was read where it is used — appending the `version` step output, which happens once the GitHub release has been published — so an unset variable failed a release that had otherwise succeeded, and the failure rolled it back, deleting the release and the tag it had just created. It is now required up front, before the provisional draft release is created and before any change to the repository, where nothing has to be undone. The message is the usual one for a missing variable (`Required environment variable GITHUB_OUTPUT is missing or empty.`), replacing `Cannot set Actions step output: GITHUB_OUTPUT not set.`
- URLs that `bv release` builds from the repository URL are no longer missing the separator before their first path segment: release links (`.../Buildvanareleases/tag/1.1.10`) and file links (`.../Buildvanablob/main/CHANGELOG.md`) now come out as `.../Buildvana/releases/tag/1.1.10` and `.../Buildvana/blob/main/CHANGELOG.md`. This affected the version section titles written into the changelog and the "human-curated changelog" link at the top of every generated release description; the titles already written for 1.0.220, 1.1.4, and 1.1.10 have been corrected in place.
- `bv clean` no longer silently ignores unknown options: `bv clean --bogus` now fails with `Unknown option '--bogus' for command 'clean'`. Every `bv` command now rejects options it does not recognize, and does so before anything else runs: previously, commands that parse their own options (e.g. `bv release`) reported an unknown option only after the SDK version check, so a mismatched SDK pin could mask the typo.
- A denied or failed file or directory access during a `bv` command (e.g. a locked or read-only `CHANGELOG.md`, `Directory.Packages.props`, or public API file, or a `bin` directory locked by Visual Studio during `bv clean`) no longer surfaces as an unhandled-exception stack trace pointing at `bv` internals. File and directory accesses now report failure as a single clean error line naming the operation, the path, and the operating-system reason (`Could not read from <path>: <reason>`), and `bv` exits with its regular failure exit code. This covers failures that happen part-way through reading a file, not just failures to open it: `bv release` reads the whole of `CHANGELOG.md` before rewriting it, so a file yanked mid-read (say, by a cloud-sync provider) is reported the same clean way.
- The release date in the changelog section titles written by `bv release` is now always formatted with the Gregorian calendar and the invariant date format. On a machine whose culture prescribes a different calendar (e.g. `th-TH`, `ar-SA`), the date written into `CHANGELOG.md` was the current culture's rendering of the day (`2569-04-27` rather than `2026-04-27`), and neither matched the release tag nor sorted with the other section titles.

### Known problems introduced by this release

- When a release introduces no other file changes (typically a prerelease with no version-spec change and no changelog or public-API updates), the "Prepare release" commit is empty. The major public Git hosts accept empty commits, but self-hosted setups with custom `pre-receive` hooks may reject them. If this affects you:
  - either allow empty commits, or
  - set `release.changelogUpdates` to `all` in `buildvana.json`, so that every release updates the changelog and its commit is therefore never empty. Since an empty "Unreleased changes" section fails a release that would update the changelog, you must also either ensure the changelog always has at least one new entry between releases, or provide substitute text in `release.emptyChangelog`.

  If neither option is acceptable for your workflow, please open an issue.

## [1.1.10](https://github.com/Tenacom/Buildvana/releases/tag/1.1.10) (2026-04-27)

### Bugs fixed in this release

- [Issue #243](https://github.com/Tenacom/Buildvana/issues/243) — `dotnet bv release` now rewrites `global.json`, `.config/dotnet-tools.json`, and `version.json` in place via a byte-level splice, preserving line endings, trailing newlines, indentation, comments, and BOM exactly as they were on disk. Previously, automatic dogfooding (and version-file updates) re-serialized the entire JSON document, producing all-lines-changed diffs and dropping the trailing newline.

## [1.1.4](https://github.com/Tenacom/Buildvana/releases/tag/1.1.4) (2026-04-27)

### New features

- `dotnet bv release` now updates `global.json`, `.config/dotnet-tools.json`, and `Directory.Packages.props` references to packages produced by the current build, so a self-hosting (dogfooded) project picks up the new version as part of the "Prepare release" commit.
As a consequence, checking out a version tag and rebuilding your repository's solution will **not** reproduce the build that was originally released — the tagged commit references the just-released SDK/tool versions, while the original release was built against the previously-published versions. Whether this matters depends on your project.
To skip automatic dogfooding, either pass `--updateSelfReferences=false` to `dotnet bv release`, or set the `UPDATE_SELF_REFERENCES` environment variable to `false` in your CI workflow.

### Bugs fixed in this release

- Repository links have been fixed: all references to READMEs, changelog, and the repository itself should now bear no trace of the old `Buildvana` organization and `Buildvana.Sdk` repository.

### Known problems introduced by this release

- [Issue #243](https://github.com/Tenacom/Buildvana/issues/243) — Automatic dogfooding, introduced with [PR #242](https://github.com/Tenacom/Buildvana/pull/242), does not preserve line endings, traling newlines, and any non-standard formatting in `global.json` and `.config/dotnet-tools.json` if it happens to modify them.

## [1.0.220](https://github.com/Tenacom/Buildvana/releases/tag/1.0.220) (2026-04-27)

### New features

- No more Cake build scripts:  Buildvana now has its own global CLI tool, `bv`. More info and documentation incoming.

### Changes to existing features

- The minimum supported version of the .NET SDK is now 10.0.202
- The minimum supported version of Roslyn is now 5.3
- The minimum supported version of Visual Studio is now VS2026 18.4
- Compiled tasks are now built for .NET 10 and [used out-of-process](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/sdk#use-net-msbuild-tasks-with-net-framework-msbuild) when MSBuild runs on .NET Framework.

### Bugs fixed in this release

- Additional assembly info generation failed on Visual Basic projects, because the source generator depended on `Microsoft.CodeAnalysis.CSharp`. It now depends on `Microsoft.CodeAnalysis.Common` instead, which is available in all Roslyn compilations.

## [1.0.154-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.154-preview) (2024-04-20)

### New features

- Buildvana SDK now supports loading a machine- and/or user-scoped configuration file named `Buildvana.Sdk.props`. Please refer to the relevant [documentation](docs/SdkConfigurationFiles.md) for details.
- A `.pfx` file used to sign an assembly through the `AssemblySigning` module can now have no password. Previous versions issued an error if the `PfxPassword` property was empty or not defined.
(Please note that the `PfxPassword` property has also been renamed to `AssemblyOriginatorKeyPassword`, as noted below in the "Changes to existing features" section.)
- Some support has been added for running Windows-only tools under [Wine](https://winehq.org) when building under Linux or macOS. Please refer to the relevant [documentation](docs/modules/Wine.md) for details.
- In Inno Setup support, when there is no `AssemblyTitle` property, `AppFullName` now defaults to `AssemblyName`.
- Inno Setup's compiler can now run on macOS and Linux (through Wine):
  - Wine support must be configured at machine / user level - please refer to the documentation for [configuration files](docs/SdkConfigurationFiles.md) and the [`Wine` module](docs/modules/Wine.md);
  - `InnoSetupConstant` items whose `Value` metadata is a filesystem path must have an `IsPath="true"` metadata, so that paths are converted to Windows-style paths when using Wine.

### Changes to existing features

- The minimum supported version of Roslyn is now 4.9
- The minimum supported version of Visual Studio is now VS2022 17.9
- The minimum supported version of the .NET SDK is now 8.0.200
- The `AssemblySigning` module now expects the password to use for the `.pfx` file in the `AssemblyOriginatorKeyPassword` (as opposed to `PfxPassword`) property.

### Bugs fixed in this release

- The `AssemblySigning` module did not work any more, due to the removal of `Buildvana.Sdk.Tasks.dll` in version 1.0.0-alpha.21. The compiled tasks have been brought back and are now compiled for .NET Standard 2.0, so that the same DLL can be used for both Visual Studio and the .NET SDK.
- Due to a typo in Inno Setup support code, `ReleaseAssetDescription` metadata for `InnoSetup` items were not honored and the default asset description was always used.
- `AppShortName` and `AppFullName` properties were not honored by Inno Setup support code; `AssemblyName` was used in their place.

## [1.0.131-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.131-preview) (2024-01-21)

This release just updates some dependencies to their latest versions, the most notable of them being `StyleCop.Analyzers`, brought up to 1.2.0-beta.556.

## [1.0.122-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.122-preview) (2023-11-22)

### Bugs fixed in this release

- Although the minimum supported Roslyn version was 4.8, version 1.0.116-preview still depended on version 4.7 of `Microsoft.CodeAnalysis.CSharp`. The dependency version has now been properly updated.

## [1.0.116-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.116-preview) (2023-11-17)

### Changes to existing features

- The minimum supported version of Roslyn is now 4.8
- The minimum supported version of Visual Studio is now VS2022 17.8
- The minimum supported version of the .NET SDK is now 8.0.100

## [1.0.110-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.110-preview) (2023-11-09)

### Changes to existing features

- The change in version 1.0.106-preview, whereas the `Title` property was used  as a default for `AssemblyTitle`, has been reversed. It turns out that the order in which MSBuild loads Buildvana.Sdk in relation to Microsoft.NET.Sdk makes the change ineffective.
- The default value for property `AppFullName`, used as a constant passed to InnoSetup, is now `$(AssemblyTitle)` instead of `$(Title)`.
- **BREAKING CHANGE**: The property used as NuGet package title is now `PackageTitle` instead of `Title`. If left unset, it defaults to `$(AssemblyTitle)`.

## [1.0.106-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.106-preview) (2023-11-09)

### Changes to existing features

- When using either NuGet Pack support or alternate pack, the `Title` property is now used as a default for `AssemblyTitle`.

## [1.0.102-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.102-preview) (2023-11-09)

### Changes to existing features

- When using NerdBack.GitVersioning, the value of `AssemblyInformationalVersion` is now changed to not include metadata (Git commit SHA) when building a public version (i.e. from `main` or other branches identified in `version.json`).
This change also affects the default names of zipped publish folders and InnoSetup-generated setup programs, as they use `AssemblyInformationalVersion` as a suffix.

## [1.0.99-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.99-preview) (2023-11-08)

### Bugs fixed in this release

- The separator character used in default InnoSetup output names before the program version was a minus sign `-` instead of its intended value of underscore `_`.

## [1.0.94-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.94-preview) (2023-11-04)

### Bugs fixed in this release

- InnoSetup would fail if the script specified in the `Script` metadata of an `InnoSetup` item was not in the same folder as the project.

## [1.0.88-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.88-preview) (2023-10-26)

### New features

- NuGet-related features provided by the `NuGetPack` module can now be disabled altogether by setting the `IncludeNuGetPackSupport` property to `false`. The default value is `true`, which behaves like previous versions.

### Changes to existing features

- **BREAKING CHANGE:** Alternate pack methods can now be used independently of one another. Therefore the `AlternatePackMethod` property has been discontinued; to enable alternate pack, just set `UseAlternatePack` to `true`, as in versions prior to 1.0.41-preview where `AlternatePackMethod` was introduced.
- **BREAKING CHANGE:** InnoSetup support has been completely rewritten. Main features include the following:
  - Creation of an InnoSetup installations is no longer bound to a `PublishFolder`.
  - An `InnoSetup` item must be created for every installation program to create.
  - Source files for an installation can come from a publish folders, or from a custom location.
  - Installation programs can be automatically added to the release asset list.
- The default name for a zipped publish folder is now suffixed with the complete informational version (the `AssemblyInformationalVersion` property) when available, including semantic versioning metadata. This allows for clearer distinction between, for example, zip files created locally and on a continuous integration server.

### Bugs fixed in this release

- It was not possible to run InnoSetup scripts for more than one runtime identifier with the same target framework. For example, given two `PublishFolder`s, both with `TargetFramework` set to `net7.0`, whose `RuntimeIdentifier`s were `win10-x86` and `win10-x64` respectively, a setup was generated only for the first of the two. This has been fixed.

## [1.0.75-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.75-preview) (2023-10-03)

### Changes to existing features

- If the `CreateZipFile` metadata of a `PublishFolder` item is `true` and its `ZipFileName` metadata is not set, the latter defaults to:
  - `$(MSBuildProjectName)-%(PublishFolder.Identity)_$(PackageVersion).zip` if the `PackageVersion` property is set
  (note that the `BuildVersion` and `AssemblyInformationalVersion` properties were previously used instead of `PackageVersion`);
  - `$(MSBuildProjectName)-%(PublishFolder.Identity).zip` otherwise
  (this has not changed).

## [1.0.72-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.72-preview) (2023-10-02)

### Changes to existing features

- If the `CreateZipFile` metadata of a `PublishFolder` item is `true` and its `ZipFileName` metadata is not set, the latter defaults to:
  - `$(MSBuildProjectName)-%(PublishFolder.Identity)_$(AssemblyInformationalVersion).zip` if the `AssemblyInformationalVersion` property is set
  (note that the `BuildVersion` property was previously used instead of `AssemblyInformationalVersion`);
  - `$(MSBuildProjectName)-%(PublishFolder.Identity).zip` otherwise
  (this has not changed).

## [1.0.69-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.69-preview) (2023-10-02)

### New features

- `ReleaseAsset` items can now have their MIME type specified via the `MimeType` metadata. When not specified, the MIME type of an asset defaults to `application/octet-stream`.
- Zipped publish folders may now have a `ReleaseAssetMimeType` metadata specifying their MIME type when uploaded as a release asset. The default value is `application/zip`.

### Changes to existing features

- **BREAKING CHANGE:** The format of release asset lists has changed: each line now contains the full path of an asset, its MIME type, and its description, separated (like before) by tab characters (Unicode U+0009).

## [1.0.66-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.66-preview) (2023-10-01)

### New features

- A new property `CompletePublishFolderMetadataDependsOn` has been added. The `CompletePublishFolderMetadata` target will depend on targets listed in this property. This is useful to separate concerns among alternate pack methods.
- The new `ReleaseAssetList` module allows for creation of lists of assets to associate with a release, useful when releases are created externally (GitHub, etc.) and associated assets are the only way to retrieve published artifacts.
  - release asset list generation is enabled by the `GenerateReleaseAssetList` boolean property, defaulting to `true` except in libraries and test projects;
  - to include a file in the release asset list for a project, just add one or more `ReleaseAsset` items;
  - the `Description` metadata of `ReleaseAsset` items can be used to add a textual description of each asset, for CI systems that can use it;
  - release assets without a `Description` metadata are given a default description according to the `DefaultReleaseAssetDescription` property, whose default value is "(no description given)";
  - release asset lists are UTF-8 text files;
    - each row of a release asset list contains the full path of an asset, a tab character (Unicode U+0009), and the asset's description;
    - rows are separated by the build system's line separator (CR+LF on Windows, LF otherwise);
  - each project in a solution generates its own release asset list, whose name can be set via the `ReleaseAssetListFileName` property, defaulting to `$(MSBuildProjectName).assets.txt`;
  - all release asset lists for a solution are placed in the artifacts directory, `$(ArtifactsDirectory)$(Configuration)`.
- New metadata in `PublishFolder` items allow for zipping a published folder:
  - `CreateZipFile` (boolean) enables the creation of a ZIP file with the contents of the published folder;
  - `ZipFileName` (string) is the name (complete with extension) of the created ZIP file;
  - ZIP files are created in the artifacts directory `$(ArtifactsDirectory)$(Configuration)`;
  - the same publish folder can be zipped _and_ used by InnoSetup, if needed;
  - if `Temporary` is set to `true` on a publish folder, it will be deleted after zipping (and after running InnoSetup if required);
  - zipped publish folders are added to the release asset list for the project by default, unless their `IsReleaseAsset` metadata is set to `false`;
  - `ReleaseAssetDescription` metadata can be set to the textual description for the zipped folder in the release asset list;
  - `CreateZipFile` defaults to `true` if `ZipFileName` is set, `false` otherwise;
  - if `CreateZipFile` is `true` and `ZipFileName` is not set, the latter defaults to:
    - `$(MSBuildProjectName)-%(PublishFolder.Identity)_$(BuildVersion).zip` if the `BuildVersion` property is set (such as when using Nerdbank.GitVersioning);
    - `$(MSBuildProjectName)-%(PublishFolder.Identity).zip` otherwise.

### Changes to existing features

- The minimum supported version of Roslyn is now 4.7
- The minimum supported version of Visual Studio is now VS2022 17.7
- The minimum supported version of the .NET SDK is now 7.0.401
- When not using `Nerdbank.GitVersioning`, a stub `GetBuildVersion` target is added to the project. This allows other targets to depend on `GetBuildVersion`. Care should be exercised, however, to check that version-related properties have actually being set.
- The `GetBuildVersion` target is always invoked before packing when using any alternate pack method. This ensures that properties such as `BuildVersion`, `InformationalVersion`, etc. are available to packing sub-modules, at least when using Nerdbank.GitVersioning.

### Bugs fixed in this release

- An item group called `InnoSetupIncludeLine`, used internally by the `AlternatePack` module when the `AlternatePackMethod` property is set to `InnoSetup`, was meant to be cleared after use to free up some memory, but wasn't actually cleared. This has been fixed.

## [1.0.51-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.51-preview) (2023-08-02)

### Changes to existing features

- The minimum supported version of Roslyn is now 4.6
- The minimum supported version of Visual Studio is now VS2022 17.6
- The minimum supported version of the .NET SDK is now 7.0.306
- The following automatically added dependencies have been updated:
  - `Jetbrains.Annotations` to version 2023.2.0
  - `Nerdbank.GitVersioning` to version 3.6.133
  - `StyleCop.Analyzers` to version 1.2.0-beta.507

## [1.0.41-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.41-preview) (2023-07-18)

### New features

- **BREAKING CHANGE:** The `UseAlternatePack` property is no longer recognized. Projects must instead set `AlternatePackMethod` to one of the following values:
  - `None`: does nothing (useful to silence warnings in library projects using `Microsoft.Net.Sdk.Web`)
  - `PublishToFolders`: publish to folders, no InnoSetup involved
  - `InnoSetup`: publish to folders and generate setup (this is the value to use in projects that previously set `UseAlternatePack` to `true`)

## [1.0.26-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.26-preview) (2023-05-02)

This version just updates all dependencies, as well as build scripts.

## [1.0.13-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.13-preview) (2022-11-25)

### Bugs fixed in this release

- Version 1.0.7-preview contained a syntax error in a `.targets` file.
- When using version 1.0.7-preview, Restore failed because of a missing Buildvana.Sdk v1.0.0 package.

## [1.0.7-preview](https://github.com/Tenacom/Buildvana/releases/tag/1.0.7-preview) (2022-11-25) **_RETIRED_**

### New features

- The [`Nerdbank.GitVersioning`](https://github.com/dotnet/Nerdbank.GitVersioning) package is now automatically referenced if either a `version.json` or a `.version.json` file is found looking from the project directory up until `HomeDirectory`. To disable this behavior, set `UseNerdbankGitVersioning` to `false` either in your project file or in a `Common.props` file. To issue an error `BVSDK2000` if a version JSON file is _not_ found, set `UseNerdbankGitVersioning` to `true` either in your project file or in a `Common.props` file.

### Changes to existing features

- Errors and warnings issued by Buildvana SDK are no longer prefixed differently: `BVSDK` is the new prefix for all diagnostics.

### Known problems introduced by this release

- This version contains a syntax error in a `.targets` file that somehow slipped through to distribution.
- Because of a weird interaction between the `Microsoft.Build.NoTargets` SDK and `Nerdbank.GitVersioning` when packing without building (which our CI workflows, as luck would have it, do) this version will try to reference version 1.0.0 of itself - which still doesn't exist - and restore will fail every time.

## [1.0.0-alpha.23](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.23) (2022-11-13)

### New features

- TFM-specific public API files (`PublicAPI\$(TargetFramework)\PublicAPI.{Shipped|Unshipped}.txt`) can now be disabled for multi-target projects by setting the `UseTfmSpecificPublicApiFiles` property to `false`. They can also be enabled for non-multi-target files by setting the same property to `true`.

## [1.0.0-alpha.22](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.22) (2022-09-22)

### New features

- Quite a few more properties are exported to the external `NuSpecFile`. The complete list can be seen [in the source code](https://github.com/Tenacom/Buildvana/blob/main/src/Buildvana.Sdk/Modules/NuGetPack/NuspecFile.targets). A notable addition is `configuration`, the only default "nuspec property" missing in Buildvana SDK so far.
- Files used for the generation of a NuGet package are now shown in Visual Studio's Solution Explorer tree view, under a "virtual" folder named "- Package". This includes: the license file, the third-party notice file, the Readme file, the package icon, and the `NuspecFile` if specified either explicitly or implicitly (i.e. by having a `{ProjectName}.nuspec` file in the project folder).
- When using public API analyzers, [TFM-specific public API files](https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/PublicApiAnalyzers.Help.md#conditional-api-differences) are added to the project automatically.

### Changes to existing features

- When generating a NuGet package, previous versions of Buildvana SDK wrote messages to the build log specifying the full paths of license, third-party notice, readme, and icon files. These messages have been removed in favor of showing the files in Visual Studio's Solution Explorer.

### Bugs fixed in this release

- When using an external `.nuspec` file, the `$configuration$` tag did not work in previous versions of Buildvana SDK. This has been fixed.
- When using an external `.nuspec` file, the `$repositoryType$` tag did not work in previous versions of Buildvana SDK. This has been fixed.

## [1.0.0-alpha.21](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.21) (2022-09-20)

### New features

### Changes to existing features

- https://github.com/Tenacom/Buildvana/pull/158 - **BREAKING CHANGE:** The LiteralAssemblyAttributes module has been removed. The `CLSCompliant` and `ComVisible` properties, however, are still supported: the corresponding assembly attributes are generated by a source generator.
- https://github.com/Tenacom/Buildvana/pull/158 - **BREAKING CHANGE:** The ThisAssemblyClass module has been removed. The recommended workaround is to use the [`ThisAssembly`](https://www.clarius.org/ThisAssembly) package.
- https://github.com/Tenacom/Buildvana/pull/163 - **BREAKING CHANGE:** Buildvana SDK does not use or recognize a version file, or otherwise determine a project's version, any longer. The suggested workaround is to use [NerdBank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning#readme), [GitVersion](https://gitversion.net), or any other similar tool.

## [1.0.0-alpha.20](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.20) (2022-05-12)

### New features

- https://github.com/Tenacom/Buildvana/pull/142 - For packable projects, Buildvana SDK will automatically find a README.md file and include it in the NuGet package. To disable this feature, set the `ReadmeFileInPackage` property to `false`.
Recognized names for the README file, in order of lookup, are: `Package-README.md`; `package-readme.md`; `NuGet-README.md`; `nuget-readme.md`; `NuGet.md`; `nuget.md`; `README.md`; `readme.md`.

### Changes to existing features

- https://github.com/Tenacom/Buildvana/pull/146 - **BREAKING CHANGE:** The Polyfills module, introduced in v1.0.0-alpha.18, has been removed.
Polyfills are a complicated topic, with lots of edge cases. They are best dealt with at a project level. The experience acquired with the Polyfills module has helped shape a polyfill library that will be open-sourced shortly (and is, needless to say, built with Buildvana SDK).
**EDIT:** [PolyKit](https://github.com/Tenacom/PolyKit#readme) has born and is even better than anticipated!

## [1.0.0-alpha.19](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.19) (2022-04-29)

### Bugs fixed in this release

- https://github.com/Tenacom/Buildvana/pull/138 - The `UsePolyfills` property was forced to `true` in all projects in version 1.0.0-aplha.18.

## [1.0.0-alpha.18](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.18) (2022-04-26)

### New features

- https://github.com/Tenacom/Buildvana/pull/135 - Buildvana SDK will, by default, include in every C# project some polyfills that let developers use latest C# features on older platforms. To disable this feature set the `UsePolyfills` property to `false`.
  - Polyfills are provided by adding a reference to the following NuGet Packages:
    - [IndexRange](https://www.nuget.org/packages/IndexRange/);
    - [Nullable](https://www.nuget.org/packages/Nullable/).
  - In addition, the following classes are added to the project on platforms where they are not part of the BCL:
    - [System.Runtime.CompilerServices.CallerArgumentExpressionAttribute](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.callerargumentexpressionattribute)
    - [System.Runtime.CompilerServices.IsExternalInitAttribute](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.isexternalinitattribute)
    - [System.Runtime.CompilerServices.SkipLocalsInitAttribute](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.skiplocalsinitattribute)
    - [System.Diagnostics.StackTraceHiddenAttribute](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.stacktracehiddenattribute) - This one is excluded from release builds, as it would have no effect anyway; it is here just to avoid preprocessor conditionals in multi-targeted projects.
    - [ValidatedNotNullAttribute](https://docs.microsoft.com/en-us/dotnet/api/microsoft.validatednotnullattribute) - This attribute, as included by Buildvana SDK, does not have a namespace and thus does not require any `using` directive. Since a lot of projects already define this attribute, and to prevent conflicts with the Visual Studio SDK, you can disable the inclusion of this attribute buy setting the `UseValidatedNotNullAttribute` property to `false`.

## [1.0.0-alpha.17](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.17) (2022-04-24)

### New features

- https://github.com/Tenacom/Buildvana/pull/132 - InnoSetup integration now automatically includes Inno Download Plugin.
- https://github.com/Tenacom/Buildvana/pull/132 - Buildvana SDK can now be used outside of a Git repository: just put a file named ".buildvana-home" in the home directory (usually the same directory as your solution file). The ".buildvana-home" file is searched for before looking for a Git submodule or repository.

### Bugs fixed in this release

- https://github.com/Tenacom/Buildvana/pull/130 - InnoSetup integration has been fixed.

## [1.0.0-alpha.16](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.16) (2022-04-01)

### Bugs fixed in this release

- When using Buildvana SDK v1.0.0-alpha.14 with .NET SDK 6.0 and using ReSharper annotations, .NET SDK 5.0 was required too, because it was needed by the `Resharper.ExportAnnotations` dependency. This version updates `Resharper.ExportAnnotations` to a version that works with .NET SDK 6, thus removing the aforementioned requirement.

## [1.0.0-alpha.15](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.15) (2022-04-01)

### New features

- https://github.com/Tenacom/Buildvana/pull/124 - Alternate Pack target: use the Pack target to publish to one or more folders and/or create setup executables with InnoSetup. See the PR for more information.

### Changes to existing features

- https://github.com/Tenacom/Buildvana/pull/123 - **POTENTIALLY BREAKING CHANGE:** The minimum supported MSBuild version is now 17.0 (.NET SDK 6.0, Visual Studio 2022 v17.0).
- https://github.com/Tenacom/Buildvana/pull/123 - **POTENTIALLY BREAKING CHANGE:** The only supported .NET environments are now .NET 6.0 or newer and .NET Framework 4.7.2 or newer. This of course refers to the build phase; you can use Buildvana SDK to target older versions of .NET, .NET Core, or .NET Framework.
- https://github.com/Tenacom/Buildvana/pull/123 - **POTENTIALLY BREAKING CHANGE:** The `AllowUnderscoresInMemberNames` property is no longer supported. Just append `;CA1707` to the `NoWarn` property instead.

## [1.0.0-alpha.14](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.14) (2021-09-13)

### Changes to existing features

- Warning NU1604 is no longer suppressed on dependencies automatically introduced in projects by Buildvana SDK. Suppressing the warning prevented a yellow triangle from appearing near the affected packages in Visual Studio 2019 until version 16.7; in version 16.11, on the contrary, the yellow triangle appears if the warning _is_ suppressed.

## [1.0.0-alpha.13](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.13) (2021-09-12)

### Changes to existing features

- **POTENTIALLY BREAKING CHANGE:** The minimum supported MSBuild version is now 16.8 (.NET SDK 5.0, Visual Studio 2019 v16.8).
- **POTENTIALLY BREAKING CHANGE:** Building with .NET Core 3.1 SDK is not supported any longer.

### Bugs fixed in this release

- https://github.com/Tenacom/Buildvana/pull/98 - XML documentation files are now correctly created (regression in versions 1.0.0-alpha.10 through 12).

## [1.0.0-alpha.12](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.12) (2021-01-19)

### Bugs fixed in this release

- https://github.com/Tenacom/Buildvana/pull/74 - Projects using Buildvana SDK now work with Omnisharp in VS Code.

## [1.0.0-alpha.11](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.11) (2021-01-19)

### Bugs fixed in this release

- https://github.com/Tenacom/Buildvana/pull/72 - False-positive BVW1400 and/or BVW1900 warnings are not raised any more.
- https://github.com/Tenacom/Buildvana/pull/72 - Properties `GenerateAssemblyCLSCompliantAttribute` and `GenerateAssemblyComVisibleAttribute` are not set any more if `GenerateLiteralAssemblyInfo` is set to `false`.
- https://github.com/Tenacom/Buildvana/pull/72 - `LiteralAssemblyAttribute` items are not generated any more if `GenerateLiteralAssemblyInfo` is set to `false`.
- https://github.com/Tenacom/Buildvana/pull/72 - Warning CS3021 ("'type' does not need a CLSCompliant attribute because the assembly does not have a CLSCompliant attribute") is not suppressed any more if `GenerateLiteralAssemblyInfo` is set to `false`.
- https://github.com/Tenacom/Buildvana/pull/72 - Literal assembly attributes are now correctly regenerated if an attribute's named parameter changes.
- https://github.com/Tenacom/Buildvana/pull/72 - `WriteLiteralAssemblyAttributes` and `WriteThisAssemblyClass` tasks are now correctly unloaded after execution.

## [1.0.0-alpha.10](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.10) (2021-01-03)

### Changes to existing features

- **POTENTIALLY BREAKING CHANGE:** The minimum supported MSBuild version is 16.7 (.NET SDK 3.1, Visual Studio 2019 v16.7).
- **BREAKING CHANGE:** The syntax for parameters of literal assembly attributes, as well as constants in "ThisAssembly" classes, has changed. The new syntax is described in [this document](docs\ConstantsSyntax.md).
- **BREAKING CHANGE:** The `Microsoft.CodeAnalysis.FxCopAnalyzers` package is not imported any more, due to its deprecation in favor of `Microsoft.CodeAnalysis.NetAnalyzers` (see [the relevant documentation](https://docs.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview) for more details).
- **BREAKING CHANGE:** The `UseStandardAnalyzers` property is not used any more. The new `UseStyleCopAnalyzers` property enables the use of the `StyleCop.Analyzers` package.
- https://github.com/Tenacom/Buildvana/pull/62 - Messages listing the icon, license file, and/or third-party copyright notice included in packages are now shown only when packing.
- https://github.com/Tenacom/Buildvana/pull/57 - Generated `ThisAssembly` classes now have [CompilerGenerated](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.compilergeneratedattribute) and [ExcludeFromCodeCoverage](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.codeanalysis.excludefromcodecoverageattribute) attributes.
- **BREAKING CHANGE:** https://github.com/Tenacom/Buildvana/pull/57 - The default for the `UseJetBrainsAnnotations` property is now `false`. The reason is that it was counterintuitive to mention JetBrains annotations in projects _not_ using them.
- Compiled tasks used to generate ThisAssembly classes and literal assembly attributes have been completely rewritten using Roslyn code generators.
- The message for error `BVE1004` now reports the minimum required MSBuild version.
- The message for warning `BVW1900` ("ThisAssembly class generation is only supported in C# and Visual Basic projects") now reports the `Language` MSBuild property value for the project.
- **POTENTIALLY BREAKING CHANGE:** Errors `BVE1900` and `BVE1901` did not make sense with [the new constant syntax](docs\ConstantsSyntax.md). They have been removed, and the old error `BVE1902` is now `BVE1900`.

### Bugs fixed in this release

- https://github.com/Tenacom/Buildvana/pull/65 - Warning BVW1900 issued on every project with a `<TargetFrameworks>` property and ThisAssembly class generation enabled.

## [1.0.0-alpha.9](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.9) (2020-10-10)

### Changes to existing features

- https://github.com/Tenacom/Buildvana/pull/51 - The automatically-added package reference to `ReSharper.ExportAnnotations.Task` has been updated to version 1.3.1.
- **POTENTIALLY BREAKING CHANGE:** https://github.com/Tenacom/Buildvana/pull/51 - The `EnableThisAssemblyClass` property has been renamed to `GenerateThisAssemblyClass` and its default value is now `false`.

### Bugs fixed in this release

- Thanks to the `ReSharper.ExportAnnotations.Task` update, building a project on a non-Windows system will no longer fail. See https://github.com/tenacom/ReSharper.ExportAnnotations/issues/23 for details.

## [1.0.0-alpha.8](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.8) (2020-10-10)

### Changes to existing features

- https://github.com/Tenacom/Buildvana/pull/47 - The automatically-added package reference to `ReSharper.ExportAnnotations.Task` has been updated to version 1.3.0.
- https://github.com/Tenacom/Buildvana/pull/49 - Compiled tasks are built for more target frameworks, to cover a larger number of build environments and MSBuild / .NET (Core) / Visual Studio versions.

### Bugs fixed in this release

- Thanks to the `ReSharper.ExportAnnotations.Task` update, building a project with `dotnet build` using .NET Core SDK v3.1 or .NET SDK 5-rc1 does not require.NET Core 2.1 to be installed any longer. See https://github.com/tenacom/ReSharper.ExportAnnotations/issues/20 for details.

## [1.0.0-alpha.7](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.7) (2020-09-28)

### New features

- https://github.com/Tenacom/Buildvana/issues/41 - Buildvana SDK now uses compiled tasks instead of inline tasks, thus improving build performance.
- https://github.com/Tenacom/Buildvana/issues/43 - Setting the `EnableDefaultThisAssemblyConstants` property to `false` suppresses creation of default constants in the `ThisAssembly` class.
- Warning [BVW1400] is now issued if literal assembly attribute generation is enabled for a project in a language that is neither C# nor Visual Basic. Previous versions silently skipped the code generation phase.
- Warning [BVW1900] is now issued if `ThisAssembly` class generation is enabled for a project in a language that is neither C# nor Visual Basic. Previous versions silently skipped the code generation phase.

### Changes to existing features

- **POTENTIALLY BREAKING CHANGE:** https://github.com/Tenacom/Buildvana/issues/44 - The `AssemblyInfo` module has been removed. Assembly attribute generation-related properties like e.g. `GenerateAssemblyInfo`, `GenerateAssemblyVersionAttribute`, etc. are not set to `true` any more at project and common files evaluation time; instead, they are left unset and defaulted to `true` later.
- **POTENTIALLY BREAKING CHANGE:** [Errors and warnings](docs/ErrorsAndWarnings.md) have been renumbered.
- **BREAKING CHANGE:** https://github.com/Tenacom/Buildvana/issues/44 - The `CLSCompliant` property is no longer set to `true` by default; it must be set explicitly in order to generate the respective assembly attribute. Projects that contain `CLSCompliant` attributes on types and members and do not set the `CLSCompliant` property will now issue warning CS3021: _'<type_or_member>' does not need a CLSCompliant attribute because the assembly does not have a CLSCompliant attribute._. To avoid the warning, set the `CLSCompliant` property to `true` (the previous default) in the project file or in a common file.
- **BREAKING CHANGE:** https://github.com/Tenacom/Buildvana/issues/44 - The `ComVisible` property is no longer set to `false` by default; it must be set explicitly in order to generate the respective assembly attribute. In projects that need to have all types and members of the compiled assembly hidden from COM, now you must set the `ComVisible` property to `false` (the previous default) in the project file or in a common file.

### Bugs fixed in this release

- https://github.com/Tenacom/Buildvana/issues/42 - The `ThisAssembly` class was never generated by previous versions of Buildvana SDK.

### Known problems introduced by this release

## [1.0.0-alpha.6](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.6) (2020-09-19)

### New features

- https://github.com/Tenacom/Buildvana/issues/35 - A package reference to `Microsoft.NETFramework.ReferenceAssemblies` is automatically added to projects targeting .NET Framework so they can be built on non-Windows systems, or without a .NET Targeting  Pack installed.

### Bugs fixed in this release

- https://github.com/Tenacom/Buildvana/issues/36 - Building projects with [centrally-managed package versions](https://stu.dev/managing-package-versions-centrally) now works.

## [1.0.0-alpha.5](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.5) (2020-09-17)

### Bugs fixed in this release

- Dependency `ReSharper.ExportAnnotations` has been updated to version 1.1.0. This release fixes two rather serious bugs that affected Buildvana SDK's functionality. See [their changelog](https://github.com/tenacom/ReSharper.ExportAnnotations/blob/main/CHANGELOG.md) for more information.

## [1.0.0-alpha.4](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.4) (2020-09-14)

### Changes to existing features

- https://github.com/Tenacom/Buildvana/issues/30 - The LiteralAssemblyAttributes module now works as expected.

## [1.0.0-alpha.3](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.3) (2020-09-14)

### New features

- **POTENTIALLY BREAKING CHANGE:** https://github.com/Tenacom/Buildvana/issues/26 - A unit test project is now recognized as such, by convention, if its name ends with `.Tests`.
  To opt out of this convention, explicitly set `IsTestProject` to `true` or `false`.

### Changes to existing features

- Dependency `StyleCop.Analyzers` has been updated to version 1.2.0-beta.205

## [1.0.0-alpha.2](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.2) (2020-09-13)

### New features

- https://github.com/Tenacom/Buildvana/issues/22 - Warning CA1707 (Identifiers should not contain underscores) is now suppressed by default in test projects. You can control this feature via the `AllowUnderscoresInMemberNames` property.

## [1.0.0-alpha.1](https://github.com/Tenacom/Buildvana/releases/tag/1.0.0-alpha.1) (2020-09-12)

Initial release.
