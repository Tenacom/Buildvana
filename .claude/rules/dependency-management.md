# Dependency management

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

- `Common.props` / `BV_MinRoslynVersion` — minimum Roslyn version (`major.minor`)
- `Common.props` / `BV_MinRoslynVersionHint` — minimum Roslyn + VS version as diagnostic text
- `Common.props` / `BV_SourceGeneratorsPackageFolder` — source generators package folder, derived from min Roslyn version
- `src/Buildvana.Sdk/Sdk/Sdk.props` / `BV_MinMSBuildVersion` — minimum MSBuild version (`major.minor`)

## Other dependencies

- `global.json` — .NET SDK version;
- `.config/dotnet-tools.json` — .NET global tool versions (e.g. `bv`, `ngbv`).

## How to update dependencies

- Update to the latest stable .NET SDK version in `global.json` if needed.
- Use `dotnet tool update --local --all` to update .NET CLI tools.
- Use `dotnet package list --outdated --format json` to check for outdated packages.
  - If a dependent package is currently at a non-stable (preview) version, check if a stable version has been released before updating. If not, update to the latest preview version.
- `BV_PackageVersion` items in `src/Buildvana.Sdk/Sdk/PackageVersions.props` must be checked one by one, using the procedure described in `nuget-version-lookup.md`. The .props file must be updated to reflect the new versions.
