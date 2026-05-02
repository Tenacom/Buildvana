# Architecture

This is Buildvana, a build system for .NET projects built on top of MSBuild and Roslyn.
It consists of an MSBuild SDK working in addition to the SDK specified in projects, and a .NET CLI global tool (`bv`) that serves as a wrapper around common MSBuild targets and higher-level build operations.

## Sub-project structure

Sub-projects under `src/`:

- `Buildvana.Core.Abstractions` — host-agnostic contracts shared by Buildvana libraries (currently `IBuildHost`). No host references (no Cake, no MSBuild).
- `Buildvana.Core.Json` — JSON loading, parsing, saving, and in-place rewriting helpers built on `IBuildHost`.
- `Buildvana.Sdk.Tasks` — compiled MSBuild tasks.
- `Buildvana.Sdk.SourceGenerators` — Roslyn source generators.
- `Buildvana.Sdk` — MSBuild SDK. Packages the above two projects and contains the SDK props/targets.
- `Buildvana.Tool` — .NET CLI tool (`bv`).

### Project tiers

Project names follow a three-tier convention:

 - `Buildvana.Core.*` — internal libraries shared between sibling projects in this repo. Not packaged. May depend on other `Buildvana.Core.*` libraries and ordinary BCL/NuGet dependencies, but must remain host-agnostic: no host references (Cake, MSBuild, etc.). See "Core tier layout" below for how the tier is structured.
- `Buildvana.Sdk.*` — the MSBuild SDK and its components (tasks, source generators). Only `Buildvana.Sdk` is packaged; it bundles the others.
- `Buildvana.Tool` — the `bv` .NET CLI global tool. Packaged as a `dotnet tool`.

### `.Abstractions` discipline

`*.Abstractions` libraries (henceforth "abstraction libraries") contain contracts (interfaces and/or abstract base classes), plus implementation-independent helpers, provided as extension methods on contracts.

Helpers provided in an abstraction library are part of the contract for callers, but do not have to be implemented every time.
Example: method `Log(string message)` is part of the contract; extension method `Log(CompositeFormat format, params ReadOnlySpan<object?> args)` formats the message and calls the contract's `Log`.

The root namespace of an astraction library does not include the `.Abstractions" suffix. Example: the root namespace of `Buildvana.Core.Abstractions` is `Buildvana.Core`.

### Core tier layout

The `Buildvana.Core.*` tier follows a flat-by-default layout. Areas live together until there is a concrete reason to split them.

- `Buildvana.Core.Abstractions` — single shared abstractions library for the entire Core tier. Holds contracts (interfaces, abstract base classes), the helpers prescribed by the `.Abstractions` discipline above, and trivial null/no-op stubs (stateless, allocation-free) suitable as default arguments or in tests.
- `Buildvana.Core.X` — concrete implementation of area `X`. Created only when an area actually has a common implementation. Some areas (e.g., `IBuildHost`) have no common concrete implementation and therefore have no `Buildvana.Core.X` project.
- `Buildvana.Core.X.<discriminator>` — alternative concrete implementation of area `X`. Created only when a second implementation actually exists; do not pre-create.
- `Buildvana.Core.Testing` — single shared library for stateful test doubles (capture-and-assert fakes, recorders). Created lazily, on first need. Stateful fakes never go into the abstractions library.

#### Promotion triggers

An area `X` is promoted to its own `Buildvana.Core.X.Abstractions` library when _any_ of these holds:

1. A second concrete implementation actually exists (multiple `Buildvana.Core.X.<discriminator>` projects).
2. The contract needs to surface a non-BCL type in a public signature, which would otherwise pull that dependency into every consumer of the shared abstractions library.
3. The contract has a meaningfully different evolution cadence or consumer set from the rest of the Core abstractions.

Until at least one of these holds, the area's contracts live in `Buildvana.Core.Abstractions`. When an area is promoted, its capture-fakes (if any) likewise move from `Buildvana.Core.Testing` to `Buildvana.Core.X.Testing`.

#### Hygiene

- Contracts in `Buildvana.Core.Abstractions` should prefer BCL-only types in public signatures (`Stream`, `string`, primitives) over package-specific types when there is a choice. This delays promotion pressure and keeps the shared dependency footprint minimal.
- Null/no-op stubs go in `Buildvana.Core.Abstractions` (matches the `Microsoft.Extensions.*` pattern, e.g., `NullLogger`).
- Capture/recording test doubles go in `Buildvana.Core.Testing`, never in abstractions.

## Target platforms

`Buildvana.Sdk.SourceGenerators` targets `$(SourceGeneratorsTfm)` (`netstandard2.0`) as per Roslyn analyzer requirements.
`Buildvana.Sdk` does the same, because its package contains source generators from the former project.

All other projects target `$(StandardTfm)` (`netX.0` form, tracks the latest .NET LTS).

## Self-hosting

Buildvana builds itself using the last published version of its own packages (obtained from either nuget.org if stable, or a private NuGet feed if preview).
Changes to the SDK won't break the current build, but will affect the next build after the new version is published and consumed.
