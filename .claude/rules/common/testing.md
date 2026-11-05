# Testing and code coverage

This file is the same in every repository that copies `.claude`. A fact specific to one repository lives in another rule file.

`bv test` produces the coverage reports, through Microsoft.Testing.Extensions.CodeCoverage, in cobertura format, one per test project and target framework in `TestResults/`. When the repository uses a coverage tracker, such as Codecov, CI uploads them to it.

## Every build of a package gets tested

A package that targets several frameworks has one build per target framework, and each build gets tested. Two kinds of test project reach every build. `architecture.md` lists the test projects of the repository.

- The main test project of a package targets the frameworks that run a test by themselves: the newest `net<major>.<minor>` framework, and the .NET Framework target when `$(OS)` is `Windows_NT`, because a .NET Framework build runs on Windows alone.
- A `netstandard` build runs on no framework by itself. A `netstandard` test project per `netstandard` build, named after the main test project with a `.NetStandard20` or `.NetStandard21` suffix, targets the newest framework, compiles the files of the main test project, and references the package project with `SetTargetFramework="TargetFramework=netstandard2.0"` or `netstandard2.1`. It turns `DisableImplicitFrameworkDefines` on and defines the symbols of the build it tests, so that a test of framework-specific code copies the `#if` of the member it tests. The main test project has those symbols already, because its frameworks are the package's.

Two facts about `SetTargetFramework` shape a `netstandard` test project:

- NuGet restore ignores it, and follows the build nearest to the framework of the test project. A package that only the `netstandard` build references reaches the output directory only when the test project references it itself.
- The SDK adds the project references of a referenced project to the referencing project, without `SetTargetFramework`. A `netstandard` test project of a package that references another project sets `DisableTransitiveProjectReferences` to `true`, and references every project itself.

Framework-specific code can make one call site reach different code per build. A member that a base class has as a virtual member on some frameworks only is an override there, and a new member elsewhere. A call through the base type then reaches the base code on the builds where it is a new member. A test calls such a member through the concrete type, so that every build runs the code under test. The same rule holds for every construct that binds differently per build: write the call in the form that reaches the code under test on every build. `architecture.md` names the members of the repository this rule concerns.

## Coverage exclusion policy

- **Default: code gets tested.** When something is hard to cover, first ask whether a small design change makes it testable. Extracting pure logic, such as parsing, mapping, or formatting, out of plumbing into its own type is one such change. Do not create an abstraction whose only purpose is to let a test mock the environment and assert the mock. That produces coverage, not testing.
- **`[ExcludeFromCodeCoverage]` is reserved for code whose behavior the environment owns**, not the code itself. That covers P/Invoke wrappers, process composition roots such as `Program`, and console or process plumbing that reads global process state. Never use it for logic that is untested.
- **`Justification` is mandatory.** It says why a test cannot exercise the code, as in "behavior depends on the console attached to the process". It never restates the exclusion, as in "not tested".
- **Smallest scope that fits**: method over class, class over assembly. Never assembly-wide.
- **Exclude in source, not in the configuration of the coverage tracker.** The collector honors the attribute and removes excluded code from the report's denominator, so local numbers match the badge. The exclusion also lives next to the code, together with its reason. Do not add `ignore:` path lists to a `codecov.yml`.

## Test conventions

- The test framework is TUnit on Microsoft.Testing.Platform. Use TUnit's built-in assertions, `await Assert.That(...)`, never FluentAssertions.
- A test that swaps process-global state, such as console writers or the current directory, must be marked `[NotInParallel]`.

## Tests on more than one platform

`architecture.md` names the platforms the repository supports, and the runners CI tests on. When a repository supports Windows and Linux, a test that passes on one of them only is broken. The rules below keep a test portable.

- Never write a platform-specific absolute path. `C:\elsewhere\file` is absolute on Windows and relative on Linux. Code under test then resolves it against its own base directory and reads a file nobody meant.
- Build every path with `System.IO.Path`, from a base the test owns: a temporary directory, or a fixture root.
- A path literal is fine where nothing resolves it, as in XML escaping, a dump round trip, or a dictionary key. Derive the path as soon as it can reach the file system.
- Linux file systems are case-sensitive. Two fixture names differing only in case are one file on Windows and two on Linux.
- Directory enumeration order is unspecified, and NTFS and ext4 disagree. Sort before asserting an order.
- Branch on `OperatingSystem.IsWindows()` only when the code under test behaves differently per platform. An example is code that reads a cache from a different directory on each. Never branch to write one path in two forms.

## Microsoft.Testing.Platform only

Test orchestration targets MTP only. `bv test`, `DotNetService.TestSolution`, and the `"test": { "runner": "Microsoft.Testing.Platform" }` entry of `global.json` all assume it, and TUnit supports nothing else.

Do not propose a VSTest fallback, dual-runner support, or a "let the project decide" abstraction. When something in the test path needs fixing, the fix uses MTP. Passing the solution with `--solution`, or invoking `dotnet test` once per test project, are examples. Coverage goes through MTP's `--coverage`, provided by Microsoft.Testing.Extensions.CodeCoverage, not through `--collect "XPlat Code Coverage"`.
