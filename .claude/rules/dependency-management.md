# Dependency management

## Baseline dependencies

`Louis` (our own general-purpose library) and `CommunityToolkit.Diagnostics` rank alongside the BCL in repo-scoped contexts, as would any other general-purpose, dependency-light utility library we adopt. Where `architecture.md` calls for a "BCL-only" dependency closure, or for BCL-only types in public signatures, types from these libraries do not count against the rule.

The exception is `Buildvana.Runtime`, whose dependency closure must stay strictly BCL: it is consumed by repository-owned hooks, so every dependency it carries becomes one of theirs.

## NuGet package dependencies

We use `Directory.Packages.props` for central package version management. It contains separate `ItemGroup`s based on the intended usage of the dependency:

- run-time dependencies;
- development dependencies;
- test dependencies (if present);
- versions of packages included to override transitive dependencies (if present).

## Packages added to projects by Buildvana SDK

Buildvana SDK automatically injects certain packages into projects.
These packages and their versions are listed in `src/Buildvana.Sdk/Sdk/PackageVersions.props` as `BV_PackageVersion` items.

## Minimum supported tool versions

Declared as MSBuild properties:

- `Directory.Packages.props` / `BV_MinRoslynVersion` — minimum Roslyn version (`major.minor`)
- `Directory.Packages.props` / `BV_MinRoslynVersionHint` — minimum Roslyn + VS version as diagnostic text
- `Directory.Packages.props` / `BV_SourceGeneratorsPackageFolder` — source generators package folder, derived from min Roslyn version
- `src/Buildvana.Sdk/Sdk/Sdk.props` / `BV_MinMSBuildVersion` — minimum MSBuild version (`major.minor`)

## Other dependencies

- `global.json` — .NET SDK version;
- `.config/dotnet-tools.json` — .NET global tool versions (e.g. `bv`, `ngbv`).

## How to update dependencies

Run `dotnet bv deps update` from the repository root. One run moves the .NET SDK version in `global.json`, the MSBuild project SDKs, the local dotnet tools, the `PackageVersion` pins, and the `BV_PackageVersion` pins of the "SDK package injections" group that `buildvana.jsonc` declares. At the end of the run, the `deps/post-update` hook (`.buildvana/hooks/deps/post-update.cs`) derives the three Roslyn floor properties from the `Microsoft.CodeAnalysis.Common` pin.

`dotnet bv deps update --check` reports what a run would do, writes nothing, and exits 1 when anything would move. Add `--all` to list every pin, not only the ones with news.

Three packages form the Buildvana family: `bv`, `Buildvana.Sdk` and `Buildvana.Runtime`. They move in lockstep, so no scope of `bv deps` manages one. `bv self-update` is the command that moves them. This repository never needs it: it dogfoods its own release, so the release pipeline re-pins the family to each published version.

`docs/DependencyManagement.md` documents the scopes, the update policies, and what `bv` counts as a pin.

Rules that hold for a manual update:

- A pin at a prerelease version tracks the latest prerelease of its own `major.minor` line. When that line goes quiet, the latest stable takes over. A line is quiet when no prerelease sits ahead of the pin. A pin is never downgraded. Resolve a one-off lookup with the procedure in `nuget-version-lookup.md`.
- Do not update tools with `dotnet tool update --local --all`. For a tool pinned to a prerelease line it insists on the latest _stable_, which is a downgrade, and it fails the whole run refusing to do it. Update each tool with `dotnet tool update <id> --local --version <version>` instead.
- To lower the Roslyn floor, downgrade the `Microsoft.CodeAnalysis.*` pins and run `dotnet bv deps update` again. The hook derives `BV_MinRoslynVersion`, `BV_MinRoslynVersionHint` and `BV_SourceGeneratorsPackageFolder` from the pin, so an edit to those three properties alone does not survive the next run.
