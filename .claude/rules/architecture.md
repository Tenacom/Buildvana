# Architecture

Buildvana is a build system for .NET projects, built on MSBuild and Roslyn. It has two parts. One is an MSBuild SDK that works alongside the SDK a project specifies. The other is `bv`, a .NET CLI global tool that wraps common MSBuild targets and higher-level build operations.

## Project structure

Production projects live under `src/`, test projects under `tests/`. `Buildvana.slnx` is the authoritative list of both.

Project names are self-describing. The tier conventions below say what a project is, what it may depend on, and whether it is packaged. This file therefore keeps no glossary of projects. A test project is named after the area under test, which is not always a production project of the same name. `Buildvana.Core.Diagnostics.Tests` covers the diagnostics types in `Buildvana.Core.Abstractions`.

### Project tiers

Project names follow a four-tier convention:

- `Buildvana.Core.*`: internal libraries shared between sibling projects in this repo, not packaged. A Core library may depend on other `Buildvana.Core.*` libraries, on `Buildvana.Runtime`, and on ordinary BCL and NuGet dependencies. It must stay host-agnostic, so it holds no host reference, MSBuild included. `Buildvana.Runtime` is allowed because its BCL-only dependency closure keeps the tier host-agnostic. See "Core tier layout" below.
- `Buildvana.Runtime`: packaged library holding the typed model of Buildvana configuration and run-time information, such as hook args and well-known paths. Hooks consume it through an SDK-supplied version pin, and so do `bv` and the SDK tasks. Its serialization is source-generated, in `BuildvanaJsonContext`, so that the same code works in file-based apps, where reflection-based JSON serialization is disabled. Its public surface is an additive-only contract. Its dependency closure must stay BCL-only, so it references no unpackaged `Buildvana.Core.*` project, and the baseline-dependency allowance in `dependency-management.md` does not extend to it. The prohibition is one-directional, and `Buildvana.Core.*` projects may reference `Buildvana.Runtime`.
- `Buildvana.Sdk.*`: the MSBuild SDK and its components, such as tasks and source generators. Only `Buildvana.Sdk` is packaged, and it bundles the others.
- `Buildvana.Tool`: the `bv` .NET CLI global tool, packaged as a `dotnet tool`.

### `.Abstractions` discipline

A `*.Abstractions` library, called an abstraction library below, contains contracts and implementation-independent helpers. A contract is an interface or an abstract base class. A helper is usually an extension method on a contract, or a free-standing static method where no contract fits.

A helper in an abstraction library is part of the contract for callers, but an implementation does not have to provide it. Example: the method `Log(string message)` is part of the contract. The extension method `Log(CompositeFormat format, params ReadOnlySpan<object?> args)` formats the message and calls the contract's `Log`.

A free-standing helper belongs here when it makes every implementation of a contract behave the same in some respect. Its inputs are plain values rather than the contract. Example: `ActivityLineFormatter.FormatStart(depth, title)` renders an activity's opening line, so that activities look the same whichever `IReporter` implementation reports them.

The root namespace of an abstraction library does not include the `.Abstractions` suffix. Example: the root namespace of `Buildvana.Core.Abstractions` is `Buildvana.Core`.

### Core tier layout

The `Buildvana.Core.*` tier is flat by default. Areas share a project until there is a concrete reason to split them.

- `Buildvana.Core.Abstractions`: the single shared abstractions library for the whole Core tier. It holds contracts, the helpers the `.Abstractions` discipline above prescribes, and trivial null or no-op stubs. A stub is stateless and allocation-free, and serves as a default argument or in tests.
- `Buildvana.Core.X`: the concrete implementation of area `X`. Create it only when the area has a common implementation. An area without one has no `Buildvana.Core.X` project.
- `Buildvana.Core.X.<discriminator>`: an alternative concrete implementation of area `X`. Create it only when a second implementation exists. Do not pre-create it.
- `Buildvana.Core.Testing`: the single shared library for stateful test doubles, such as capture-and-assert fakes and recorders. Create it on first need. Stateful fakes never go into the abstractions library.

#### Promotion triggers

Promote an area `X` to its own `Buildvana.Core.X.Abstractions` library when any of these holds:

1. A second concrete implementation exists, as several `Buildvana.Core.X.<discriminator>` projects.
2. The contract needs a non-BCL type in a public signature. In the shared library, that type would become a dependency of every consumer.
3. The contract evolves at a different pace, or serves a different set of consumers, than the rest of the Core abstractions.

Until one of these holds, the area's contracts live in `Buildvana.Core.Abstractions`. When an area is promoted, its capture fakes, if any, move from `Buildvana.Core.Testing` to `Buildvana.Core.X.Testing`.

#### Hygiene

- When there is a choice, a contract in `Buildvana.Core.Abstractions` uses BCL types in public signatures, such as `Stream`, `string`, and primitives, over package-specific types. This delays promotion and keeps the shared dependency footprint small. Baseline dependencies, listed in `dependency-management.md`, count as BCL for this purpose.
- Null and no-op stubs go in `Buildvana.Core.Abstractions`, as `NullLogger` does in `Microsoft.Extensions.*`.
- Capture and recording test doubles go in `Buildvana.Core.Testing`, never in an abstractions library.

## Target platforms

`Buildvana.Sdk.SourceGenerators` targets `$(SourceGeneratorsTfm)`, which is `netstandard2.0`, as Roslyn requires of an analyzer. `Buildvana.Sdk` does the same, because its package contains the source generators of that project.

Every other project targets `$(StandardTfm)`, a `netX.0` moniker that tracks the latest .NET LTS.

## Tool portability

`bv` must behave the same on GitHub Actions, GitLab CI, a self-hosted runner, and a developer laptop. "Portability of what we ship" in `design-principles.md` states the general rule. This section records the incident behind it.

`bv release` failed on a GitHub runner. The `Network.Push(...)` call of `LibGit2Sharp` had no `CredentialsProvider`, so it relied on whatever credentials `actions/checkout` left in `.git/config`. That mechanism has no GitLab CI equivalent, and it changed between `actions/checkout` v3 and v5. It is also invisible from our own code, so the failure looked mysterious instead of like the missing-configuration bug it was.

The lesson covers anything `bv` needs at runtime: identity, tokens, feed URLs. The configuration file is the one override mechanism. Do not add a second one that would compete with it.

## Self-hosting

Buildvana builds itself with the last published version of its own packages, from nuget.org when stable, or from a private NuGet feed when preview. A change to the SDK does not affect the current build. It affects the first build after the new version is published and consumed.
