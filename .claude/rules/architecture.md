# Architecture

This is Buildvana, a build system for .NET projects built on top of MSBuild and Roslyn.
It consists of an MSBuild SDK working in addition to the SDK specified in projects, and a .NET CLI global tool (`bv`) that serves as a wrapper around common MSBuild targets and higher-level build operations.

## Sub-project structure

Sub-projects under `src/`:

- `Buildvana.Core.Abstractions` — host-agnostic contracts shared by Buildvana libraries (currently `IBuildHost`). No host references (no Cake, no MSBuild). Not packaged: it is an internal seam between sibling projects in this repo.
- `Buildvana.Sdk.Tasks` — compiled MSBuild tasks.
- `Buildvana.Sdk.SourceGenerators` — Roslyn source generators.
- `Buildvana.Sdk` — MSBuild SDK. Packages the above two projects and contains the SDK props/targets.
- `Buildvana.Tool` — .NET CLI tool (`bv`).

### `.Abstractions` discipline

`*.Abstractions` libraries (henceforth "abstraction libraries") contain contracts (interfaces and/or abstract base classes), plus implementation-independent helpers, provided as extension methods on contracts.

Helpers provided in an abstraction library are part of the contract for callers, but do not have to be implemented every time.
Example: method `Log(string message)` is part of the contract; extension method `Log(CompositeFormat format, params ReadOnlySpan<object?> args)` formats the message and calls the contract's `Log`.

The root namespace of an astraction library does not include the `.Abstractions" suffix. Example: the root namespace of `Buildvana.Core.Abstractions` is `Buildvana.Core`.

## Target platforms

`Buildvana.Sdk.SourceGenerators` targets `$(SourceGeneratorsTfm)` (`netstandard2.0`) as per Roslyn analyzer requirements.
`Buildvana.Sdk` does the same, because its package contains source generators from the former project.

All other projects target `$(StandardTfm)` (`netX.0` form, tracks the latest .NET LTS).

## Self-hosting

Buildvana builds itself using the last published version of its own packages (obtained from either nuget.org if stable, or a private NuGet feed if preview).
Changes to the SDK won't break the current build, but will affect the next build after the new version is published and consumed.
