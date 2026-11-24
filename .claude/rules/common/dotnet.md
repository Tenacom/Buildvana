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

## Checking a package for native AOT compatibility

"Native AOT" in `design-principles.md` says when this check is due. The check publishes a scratch application with `PublishAot`, and makes the compiler analyze every member of the package.

1. Create a project under `.claude/scratchpad/`, from the template below, next to a `Program.cs` that holds one statement.
2. Set the target framework to the newest one of the project that will reference the package.
3. Add one `TrimmerRootAssembly` item per assembly of the package. Without the item, the compiler analyzes only the members the application calls.
4. Run `dotnet publish <project file>`. The package is compatible when the publish reports no IL diagnostic.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework><target framework></TargetFramework>
    <PublishAot>true</PublishAot>
    <TrimmerSingleWarn>false</TrimmerSingleWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="<package id>" Version="<version>" />
    <TrimmerRootAssembly Include="<assembly name>" />
  </ItemGroup>

</Project>
```

- `TrimmerSingleWarn` set to `false` reports each diagnostic of the package. The default reports one diagnostic per assembly.
- The publish needs the native toolchain of the platform, which [Prerequisites](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/#prerequisites) lists. When the toolchain is missing, say so instead of skipping the check.
- To check a project of the repository the same way, replace the `PackageReference` item with a `ProjectReference` item.
