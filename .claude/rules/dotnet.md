# Build and tooling

## Build commands

Run `dotnet tool restore` first, to install the local tools. Then use these commands:

- `dotnet bv clean`: remove all build artifacts, intermediate output, and temporary files.
- `dotnet bv restore`: restore NuGet packages.
- `dotnet bv build`: build the solution.
- `dotnet bv test`: run the tests.
- `dotnet bv pack`: create the NuGet packages in `artifacts/`.

Each command includes the ones before it: `dotnet bv build` also cleans and restores, `dotnet bv test` also builds, and so on.

### Capturing build/test output

`bv` streams a large amount of child-process output line by line. The agent's shell tools truncate that output **before the final summary**, the `Build succeeded` line and the warning and error counts. So the outcome of `dotnet bv build` or `dotnet bv test` is not readable from the tail.

To verify a build, run a plain `dotnet build` through PowerShell and keep only the tail:

```powershell
dotnet build Buildvana.slnx -v m | Select-Object -Last 25
```

This shows the per-project outputs plus the summary. Use `dotnet bv build`, `pack`, or `test` when you need the full clean, restore, and build chain, or the artifacts. Use the direct `dotnet build` above for a quick compile-and-warning check.

## Efficiency

- Use the built-in Read, Glob, and Grep tools to examine files. Do not shell out to cat, grep, find, or similar when a built-in tool exists.
- Prefer a dotnet CLI command over a script.
- When a quick script is needed, write a single-file C# app, not Python. This is a .NET project.

## Inspecting third-party library internals

When you need to know what a NuGet package does, such as a method's behavior or its argument handling:

- Retrieve the package information with `dotnet package search <package_name> --exact-match --prerelease --verbosity detailed --format json`. The output usually holds the project URL next to the current version, and the URL is often a GitHub repository.
- **Read the source on GitHub.** Fetch it with WebFetch or a subagent. Most packages are open source and tagged by version there.
- Do not load the DLL through PowerShell reflection. Type-load failures from transitive dependencies make it unreliable on Windows.
- Do not unzip a `.nupkg` file looking for `.cs` source. A runtime package contains no source. Source packages use `.snupkg` and are rarely needed.
- Do not install or invoke an ad-hoc tool, such as `dotnet-script` or `ildasm`, unless it is confirmed present. Fetching the source is faster and always works.
