# Build and tooling

## Build commands

First of all, use `dotnet tool restore` to install global tools. Then use the following commands for common operations:

- `dotnet bv clean` - Remove all build artifacts, intermediate output, and temporary files.
- `dotnet bv restore` - Restore NuGet packages.
- `dotnet bv build` - Build the solution.
- `dotnet bv test` - Run tests.
- `dotnet bv pack` - Create NuGet packages in `artifacts/`.

Each one of the preceding commands includes the previous ones, so `dotnet bv build` also cleans and restores, `dotnet bv test` also builds, and so on.

### Capturing build/test output

`bv` streams a large amount of child-process output line by line. Captured through the agent's shell tools, this output is **truncated before the final summary** (`Build succeeded`, warning/error counts), so the result is invisible — do not run `dotnet bv build`/`test` and expect to read the outcome from the tail.

To verify a build, run a plain `dotnet build` through PowerShell and keep only the tail:

```powershell
dotnet build Buildvana.slnx -v m | Select-Object -Last 25
```

This shows the per-project outputs plus the `Build succeeded` / warning / error summary. Use `dotnet bv build` (or `pack`/`test`) when you actually need the full clean + restore + build chain or the artifacts; use the direct `dotnet build` above for a quick compile-and-warning check.

## Efficiency

- Use built-in Read, Glob, and Grep tools to examine files. Do not shell out to cat, grep, find, or similar when built-in tools exist.
- Prefer dotnet CLI commands over writing scripts whenever possible.
- This is a .NET project. When a quick script is needed, write a single-file C# app, not Python.

## Inspecting third-party library internals

When you need to understand what a NuGet package actually does (method behavior, argument handling, etc.):

- Retrieve package information using `dotnet package search <package_name> --exact-match --prerelease --verbosity detailed --format json`. Alongside the current package version you'll probably find the project URL, which is often a GitHub repository.
- **Go straight to GitHub.** Fetch the source using WebFetch or a subagent. Most packages are open source and tagged by version on GitHub.
- Do NOT attempt PowerShell/reflection on the DLL — type-load failures from transitive dependencies make this unreliable on Windows.
- Do NOT try to unzip `.nupkg` files looking for `.cs` source — runtime packages do not contain source. Source packages use `.snupkg` and are rarely needed.
- Do NOT install or invoke ad-hoc tools (`dotnet-script`, `ildasm`, etc.) unless already confirmed present; fetching source is faster and always works.
