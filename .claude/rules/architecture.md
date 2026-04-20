# Architecture

This is Buildvana, a build system for .NET projects built on top of MSBuild and Roslyn.
It consists of an MSBuild SDK working in addition to the SDK specified in projects, and a .NET CLI global tool (`bv`) that serves as a wrapper around common MSBuild targets and higher-level build operations.

## Sub-project structure

Sub-projects under `src/`:

- `Buildvana.Sdk.Tasks` — compiled MSBuild tasks.
- `Buildvana.Sdk.SourceGenerators` — Roslyn source generators.
- `Buildvana.Sdk` — MSBuild SDK. Packages the above two projects and contains the SDK props/targets.
- `Buildvana.Tool` — .NET CLI tool (`bv`).

## Target platforms

`Buildvana.Sdk.SourceGenerators` targets `$(SourceGeneratorsTfm)` (`netstandard2.0`) as per Roslyn analyzer requirements.
`Buildvana.Sdk` does the same, because its package contains source generators from the former project.

All other projects target `$(StandardTfm)` (`netX.0` form, tracks the latest .NET LTS).

## Self-hosting

Buildvana builds itself using the last published version of its own packages (obtained from either nuget.org or a private NuGet feed, whichever hosts the newest version).
Changes to the SDK won't break the current build, but will affect the next build after the new version is published and consumed.
