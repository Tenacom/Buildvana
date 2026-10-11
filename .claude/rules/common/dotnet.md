# .NET tooling

This file is the same in every repository that copies `.claude`. A fact specific to one repository lives in another rule file.

`buildvana.md` holds the build commands.

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
