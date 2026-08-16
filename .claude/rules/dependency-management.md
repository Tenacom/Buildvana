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

Run `dotnet run tools/update-dependencies.cs` from the repository root. In one pass it updates the .NET SDK version in `global.json`, the local dotnet tools, the `PackageVersion` and `BV_PackageVersion` pins, and the three Roslyn floor properties; `dotnet run tools/update-dependencies.cs -- --check` reports what it would do without modifying anything. Package targets are resolved with the procedure described in `nuget-version-lookup.md`, which remains the manual procedure for one-off lookups.

Rules the tool encodes, which hold for manual updates too:

- A pin currently at a non-stable (preview) version tracks the latest prerelease of its own `major.minor` line; when that line goes quiet — no prerelease ahead of the pin — the latest stable takes over. A pin is never downgraded.
- Do not update tools with `dotnet tool update --local --all`: for a tool pinned to a prerelease line (usually `bv` itself) it insists on the latest _stable_ — a downgrade — and fails the whole run refusing to do it. Update each tool with `dotnet tool update <id> --local --version <version>` instead.
- The Roslyn floor properties derive from the `Microsoft.CodeAnalysis.Common` pin: `BV_MinRoslynVersion` is its `major.minor`, `BV_SourceGeneratorsPackageFolder` follows, and `BV_MinRoslynVersionHint` names the lowest released SDK feature band (and its paired Visual Studio version) shipping a compiler at least that new. To deliberately lower the floor, downgrade the `Microsoft.CodeAnalysis.*` pins first and re-run the tool.
