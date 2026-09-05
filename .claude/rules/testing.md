# Testing and code coverage

`bv test` produces the coverage reports, through Microsoft.Testing.Extensions.CodeCoverage, in cobertura format, one per test project in `TestResults/`. CI uploads them to Codecov.

## Coverage exclusion policy

- **Default: code gets tested.** When something is hard to cover, first ask whether a small design change makes it testable. Extracting pure logic, such as parsing, mapping, or formatting, out of plumbing into its own type is one such change. Do not create an abstraction whose only purpose is to let a test mock the environment and assert the mock. That produces coverage, not testing.
- **`[ExcludeFromCodeCoverage]` is reserved for code whose behavior the environment owns**, not the code itself. That covers P/Invoke wrappers, process composition roots such as `Program`, and console or process plumbing that reads global process state. Never use it for logic that is untested.
- **`Justification` is mandatory.** It says why a test cannot exercise the code, as in "behavior depends on the console attached to the process". It never restates the exclusion, as in "not tested".
- **Smallest scope that fits**: method over class, class over assembly. Never assembly-wide.
- **Exclude in source, not in Codecov configuration.** The collector honors the attribute and removes excluded code from the report's denominator, so local numbers match the badge. The exclusion also lives next to the code, together with its reason. Do not add `ignore:` path lists to a `codecov.yml`.

## Test conventions

- The test framework is TUnit on Microsoft.Testing.Platform. Use TUnit's built-in assertions, `await Assert.That(...)`, never FluentAssertions.
- A test that swaps process-global state, such as console writers or the current directory, must be marked `[NotInParallel]`.

## Tests run on Windows and Linux

Both are supported platforms, and CI runs the suite on Linux. A test that passes on one platform only is broken.

- Never write a platform-specific absolute path. `C:\elsewhere\file` is absolute on Windows and relative on Linux. Code under test then resolves it against its own base directory and reads a file nobody meant.
- Build every path with `System.IO.Path`, from a base the test owns: a temporary directory, or a fixture root.
- A path literal is fine where nothing resolves it, as in XML escaping, a dump round trip, or a dictionary key. Derive the path as soon as it can reach the file system.
- Linux file systems are case-sensitive. Two fixture names differing only in case are one file on Windows and two on Linux.
- Directory enumeration order is unspecified, and NTFS and ext4 disagree. Sort before asserting an order.
- Branch on `OperatingSystem.IsWindows()` only when the code under test behaves differently per platform. An example is code that reads a cache from a different directory on each. Never branch to write one path in two forms.

## Microsoft.Testing.Platform only

Test orchestration targets MTP only. `bv test`, `DotNetService.TestSolution`, and the `"test": { "runner": "Microsoft.Testing.Platform" }` entry of `global.json` all assume it, and TUnit supports nothing else.

Do not propose a VSTest fallback, dual-runner support, or a "let the project decide" abstraction. When something in the test path needs fixing, the fix uses MTP. Passing the solution with `--solution`, or invoking `dotnet test` once per test project, are examples. Coverage goes through MTP's `--coverage`, provided by Microsoft.Testing.Extensions.CodeCoverage, not through `--collect "XPlat Code Coverage"`.
