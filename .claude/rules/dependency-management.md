# Dependency management

## Baseline dependencies

`Louis`, our own general-purpose library, and `CommunityToolkit.Diagnostics` rank alongside the BCL in this repository. Any other general-purpose, dependency-light utility library we adopt ranks the same. Where `architecture.md` calls for a BCL-only dependency closure, or for BCL-only types in public signatures, types from these libraries do not count against the rule.

The exception is `Buildvana.Runtime`, whose dependency closure must stay strictly BCL. Repository-owned hooks consume it, so every dependency it carries becomes one of theirs.

## NuGet package dependencies

`Directory.Packages.props` holds the central package versions. It has one `ItemGroup` per kind of dependency:

- run-time dependencies
- development dependencies
- test dependencies, when present
- versions that override transitive dependencies, when present

## Packages added to projects by Buildvana SDK

The Buildvana SDK injects some packages into every project. `src/Buildvana.Sdk/Sdk/PackageVersions.props` lists them, with their versions, as `BV_PackageVersion` items.

## Minimum supported tool versions

MSBuild properties declare them:

- `BV_MinRoslynVersion` in `Directory.Packages.props`: minimum Roslyn version, as `major.minor`.
- `BV_MinRoslynVersionHint` in `Directory.Packages.props`: minimum Roslyn and Visual Studio version, as diagnostic text.
- `BV_SourceGeneratorsPackageFolder` in `Directory.Packages.props`: source generators package folder, derived from the minimum Roslyn version.
- `BV_MinMSBuildVersion` in `src/Buildvana.Sdk/Sdk/Sdk.props`: minimum MSBuild version, as `major.minor`, derived from the Visual Studio version paired with the minimum Roslyn version.
- The `TOOLCHAIN-FLOORS` region of `docs/introduction.md`: the minimum .NET SDK, Visual Studio, and MSBuild versions, as a table, derived from the same values.

## Other dependencies

- `global.json`: the .NET SDK version.
- `.config/dotnet-tools.json`: the versions of the local dotnet tools, such as `bv` and `ngbv`.

## How to update dependencies

Run `dotnet bv deps update` from the repository root. One run moves the .NET SDK version in `global.json`, the MSBuild project SDKs, the local dotnet tools, and the `PackageVersion` pins. It also moves the `BV_PackageVersion` pins of the "SDK package injections" group that `buildvana.jsonc` declares. At the end of the run, the `deps/post-update` hook, `.buildvana/hooks/deps/post-update.cs`, derives the toolchain floors from the `Microsoft.CodeAnalysis.Common` pin: the three Roslyn floor properties in `Directory.Packages.props`, `BV_MinMSBuildVersion` in `src/Buildvana.Sdk/Sdk/Sdk.props`, and the `TOOLCHAIN-FLOORS` region of `docs/introduction.md`.

`dotnet bv deps update --check` reports what a run would do, writes nothing, and exits 1 when anything would move. Add `--all` to list every pin, not only the ones that would move.

Three packages form the Buildvana family: `bv`, `Buildvana.Sdk`, and `Buildvana.Runtime`. `bv deps` does not manage them. This repository never needs to manage them: it builds with its own release, and the release pipeline re-pins the family to each published version.

`docs/tool-commands/dependencies.md` documents the scopes, the update policies, and what `bv` counts as a pin.

Rules that hold for a manual update:

- A pin moves as far as its policy allows. `dotnet bv deps show` reports the policy of every pin, and `docs/tool-commands/dependencies.md` says where a policy comes from. To see what a pin could move to, run `dotnet bv deps update --check --all <id>`. The pin's line ends with the latest stable version the sources have, and the latest prerelease above it, when there is one. The command exits 1 when the pin would move. Do not read that exit code as a failure.
- `dotnet bv deps update <id> --to <version>` moves every pin of that id to the version, whatever its policy. It is the one way to lower a pin.
- `dotnet bv deps update <id or pattern>... --latest` moves the named pins to the latest version the sources have, whatever the kind of their policy, and keeps its prerelease flag. Packages that depend on each other at one version, such as `Microsoft.CodeAnalysis.*`, move together in one such run. One `--to` run per package writes its pin and then fails the restore of the transitive overrides, until the last run brings the pins into agreement.
- `bv deps` knows nothing about a package with no pin. To find its latest version, run `dotnet package search <id> --exact-match`. It returns the latest stable version from the repository's package sources. Add `--prerelease` to count prereleases as well.
- Do not update tools with `dotnet tool update --local --all`. For a tool pinned to a prerelease line, it picks the latest stable, which is a downgrade. It then fails the whole run instead of downgrading. Update each tool with `dotnet tool update <id> --local --version <version>` instead.
- To lower the Roslyn floor, downgrade the `Microsoft.CodeAnalysis.*` pins and run `dotnet bv deps update` again. The hook derives `BV_MinRoslynVersion`, `BV_MinRoslynVersionHint`, `BV_SourceGeneratorsPackageFolder`, `BV_MinMSBuildVersion`, and the `TOOLCHAIN-FLOORS` region from the pin, so an edit to any of them alone does not survive the next run.
