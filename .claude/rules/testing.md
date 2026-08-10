# Testing and code coverage

Coverage reports are produced by `bv test` (Microsoft.Testing.Extensions.CodeCoverage, cobertura format, one report per test project in `TestResults/`) and uploaded to Codecov by CI.

## Coverage exclusion policy

- **Default: code gets tested.** If something is hard to cover, first ask whether a small, honest design change makes it testable — e.g., extracting pure logic (parsing, mapping, formatting) out of plumbing into its own type. Do NOT create abstractions whose only purpose is to let a test mock the environment and assert the mock: that is coverage theater, not testing.
- **`[ExcludeFromCodeCoverage]` is reserved for code whose behavior is owned by the environment**, not by the code itself: P/Invoke wrappers, process composition roots (`Program`), console/process plumbing that reads global process state. Never use it for logic that is merely untested.
- **`Justification` is mandatory**, phrased as _why a test cannot honestly exercise this code_ ("behavior depends on the console attached to the process"), never as a restatement of the exclusion ("not tested").
- **Smallest scope that fits**: method over class, class over assembly; never assembly-wide.
- **Exclude in source, not in Codecov configuration.** The collector honors the attribute and removes excluded code from the report's denominator, so local numbers match the badge, and the exclusion lives next to the code together with its reason. Do not add `ignore:` path lists to a `codecov.yml`.

## Test conventions

- Test framework is TUnit on Microsoft.Testing.Platform; use TUnit's built-in assertions (`await Assert.That(...)`), never FluentAssertions.
- Tests that swap process-global state (console writers, current directory) must be marked `[NotInParallel]`.

## Microsoft.Testing.Platform only

Test orchestration targets MTP exclusively. `bv test`, `DotNetService.TestSolution`, and `global.json`'s `"test": { "runner": "Microsoft.Testing.Platform" }` all assume it, and TUnit is MTP-only by design.

Do not propose VSTest fallbacks, dual-runner support, or "let the project decide" abstractions. When something in the test path needs fixing, the fix has to be MTP-shaped — e.g. passing the solution via `--solution`, or iterating test projects and invoking `dotnet test` once per project. Coverage likewise goes through MTP's `--coverage` (Microsoft.Testing.Extensions.CodeCoverage), not `--collect "XPlat Code Coverage"`.
