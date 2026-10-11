# Dependency management

This file is specific to this repository. Rewrite it when `.claude` is copied to another repository. `buildvana.md` holds the dependency rules that hold in every repository built with Buildvana.

## Baseline dependencies

`Louis`, our own general-purpose library, and `CommunityToolkit.Diagnostics` rank alongside the BCL in this repository. Any other general-purpose, dependency-light utility library we adopt ranks the same. Where `architecture.md` calls for a BCL-only dependency closure, or for BCL-only types in public signatures, types from these libraries do not count against the rule.

The exception is `Buildvana.Runtime`, whose dependency closure must stay strictly BCL. Repository-owned hooks consume it, so every dependency it carries becomes one of theirs.

## Packages added to projects by Buildvana SDK

`src/Buildvana.Sdk/Sdk/PackageVersions.props` lists the packages Buildvana SDK injects into every project. Each package is a `BV_PackageVersion` item, with its version.

`dotnet bv deps update` moves the `BV_PackageVersion` pins too, because `buildvana.jsonc` declares them as the "SDK package injections" group.

## Minimum supported tool versions

Four MSBuild properties and one generated region declare the minimum supported tool versions:

- `BV_MinRoslynVersion` in `Directory.Packages.props`: minimum Roslyn version, as `major.minor`.
- `BV_MinRoslynVersionHint` in `Directory.Packages.props`: minimum Roslyn and Visual Studio version, as diagnostic text.
- `BV_SourceGeneratorsPackageFolder` in `Directory.Packages.props`: source generators package folder, derived from the minimum Roslyn version.
- `BV_MinMSBuildVersion` in `src/Buildvana.Sdk/Sdk/Sdk.props`: minimum MSBuild version, as `major.minor`, derived from the Visual Studio version paired with the minimum Roslyn version.
- The `TOOLCHAIN-FLOORS` region of `docs/introduction.md`: the minimum .NET SDK, Visual Studio, and MSBuild versions, as a table, derived from the same values.

At the end of every `dotnet bv deps update` run, the `deps/post-update` hook, `.buildvana/hooks/deps/post-update.cs`, derives every value in the list above from the `Microsoft.CodeAnalysis.Common` pin. A manual edit to one of the values does not survive the next run.

To lower the Roslyn floor, downgrade the `Microsoft.CodeAnalysis.*` pins and run `dotnet bv deps update` again.

## The Buildvana family

This repository builds with the last published release of the Buildvana family. The release pipeline re-pins the family to each published version. Never run `dotnet bv self-update` here.
