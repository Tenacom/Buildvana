# `Wine` module

This module runs Windows-only build tools through [Wine](https://winehq.org) when the build runs on Linux or macOS.
It checks that a Wine command is configured, and converts host paths to the Windows paths the tools expect.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Configuration](#configuration)
  - [`WineCommand` property](#winecommand-property)
- [Usage](#usage)
  - [`NeedWine` items](#needwine-items)
  - [`UseWine` property](#usewine-property)
  - [Converting paths](#converting-paths)
    - [`GetWinePath` task](#getwinepath-task)
    - [`GetWinePaths` task](#getwinepaths-task)
    - [`ConvertToWinePaths` task](#converttowinepaths-task)
  - [Invoking a tool through Wine](#invoking-a-tool-through-wine)
- [Diagnostics](#diagnostics)

---

## Configuration

### `WineCommand` property

The command that runs a tool through Wine.
Set it in a [Buildvana SDK configuration file](../sdk-configuration-files.md#buildvanasdkprops), because the command depends on the machine.

A plain `wine SomeTool.exe param1 param2` seldom suffices.
Wine reads its configuration from environment variables such as `WINEPREFIX`, and a build may need a Wine prefix of its own.
The property holds a command prefix, with the environment variable assignments it needs:

```xml
<PropertyGroup>
  <WineCommand>WINEPREFIX=~/.alternateWineConfiguration wine</WineCommand>
</PropertyGroup>
```

It may also hold the path of a script to the same effect:

```xml
<PropertyGroup>
  <WineCommand>/usr/local/bin/run-wine</WineCommand>
</PropertyGroup>
```

[`buildvana-builder`](https://github.com/Tenacom/buildvana-builder) holds a script of that kind.
It is a Docker image based on Ubuntu LTS, with the .NET SDK and the command-line compiler of [Inno Setup](https://jrsoftware.org/isinfo.php).

---

## Usage

### `NeedWine` items

Declare a `NeedWine` item for every Windows-only tool the build runs, outside any target:

```xml
<ItemGroup>
  <NeedWine Include="ToolName" />
</ItemGroup>
```

The name is informational, and need not be the name of the executable.
When at least one `NeedWine` item exists on Linux or macOS, and `WineCommand` is empty after evaluation, Buildvana SDK raises error BVSDK2200.
The error lists the names of the tools.

### `UseWine` property

Buildvana SDK sets `UseWine` to `true` in an initial target, when the build runs on Linux or macOS and at least one `NeedWine` item exists.
On Windows, or without a `NeedWine` item, it sets the property to `false`.
During evaluation the property is empty, so read it in a target only.

### Converting paths

A tool takes paths to its input files, output files, and configuration files.
MSBuild [property functions](https://learn.microsoft.com/en-us/visualstudio/msbuild/property-functions) build and combine such paths, until Wine enters the picture.

A tool that runs through Wine sees a Windows file system, and expects Windows paths, with a drive letter and backslashes.
Wine itself expects the path of the executable as a Windows path.
MSBuild runs on the host and behaves as a Unix program.
It [accepts a backslash as a path separator](https://github.com/dotnet/msbuild/issues/1024), and it knows nothing about drive letters.

Wine maps the `Z:` drive to the root of the host file system by default, so a full path such as `/usr/share/some/path` becomes `Z:\usr\share\some\path`.
A property function can do the conversion:

```xml
<PropertyGroup>
  <MyFullPath>Z:$(MyFullPath.Replace('/', '\\'))</MyFullPath>
</PropertyGroup>
```

A relative path, such as one relative to the project directory, becomes a full path first:

```xml
<PropertyGroup>
  <MyPath>$([System.IO.Path]::Combine('$(MSBuildProjectDirectory)', '$(MyPath)'))</MyPath>
  <MyPath>Z:$(MyPath.Replace('/', '\\'))</MyPath>
</PropertyGroup>
```

The three tasks below do the same with less code.
Each one takes a host path, makes it a full path, and returns it with the `Z:` prefix and backslashes.
On Windows, each one returns the full path unchanged.

#### `GetWinePath` task

Converts one path.
`HostPath` is required:

```xml
<GetWinePath Condition="$(UseWine)"
             HostPath="$(MyPath)">
  <Output TaskParameter="WinePath" PropertyName="MyPath" />
</GetWinePath>
```

A relative `HostPath` resolves against the optional `BasePath`:

```xml
<GetWinePath Condition="$(UseWine)"
             BasePath="$(MSBuildProjectDirectory)"
             HostPath="$(MyPath)">
  <Output TaskParameter="WinePath" PropertyName="MyPath" />
</GetWinePath>
```

The `Condition="$(UseWine)"` attribute keeps the task from running on Windows.

#### `GetWinePaths` task

Converts up to ten paths in one call, from `HostPath1` to `HostPath10`, into `WinePath1` to `WinePath10`.
The optional `BasePath` applies to all of them:

```xml
<GetWinePaths Condition="$(UseWine)"
              BasePath="$(MSBuildProjectDirectory)"
              HostPath1="$(MyPath1)"
              HostPath2="$(MyPath2)"
              HostPath3="$(MyPath3)"
              HostPath4="$(MyPath4)"
              HostPath5="$(MyPath5)">
  <Output TaskParameter="WinePath1" PropertyName="MyPath1" />
  <Output TaskParameter="WinePath2" PropertyName="MyPath2" />
  <Output TaskParameter="WinePath3" PropertyName="MyPath3" />
  <Output TaskParameter="WinePath4" PropertyName="MyPath4" />
  <Output TaskParameter="WinePath5" PropertyName="MyPath5" />
</GetWinePaths>
```

#### `ConvertToWinePaths` task

Converts the paths held by an item group.
`Items` is required, `BasePath` is optional, and the converted items come out of `ConvertedItems`.
Do not send the output back to the input item group directly, because MSBuild appends output items to the existing ones.
Remove the originals first:

```xml
<ConvertToWinePaths Condition="$(UseWine)"
                    BasePath="$(MSBuildProjectDirectory)"
                    Items="@(MyPaths)">
  <Output TaskParameter="ConvertedItems" ItemName="MyConvertedPaths" />
</ConvertToWinePaths>

<!-- Copy the converted paths back to MyPaths, then empty MyConvertedPaths -->
<ItemGroup Condition="$(UseWine)">
  <MyPaths Remove="@(MyPaths)" />
  <MyPaths Include="@(MyConvertedPaths)" />
  <MyConvertedPaths Remove="@(MyConvertedPaths)" />
</ItemGroup>
```

With `MetadataName`, the task converts the named metadata of each item instead of its identity:

```xml
<ConvertToWinePaths Condition="$(UseWine)"
                    BasePath="$(MSBuildProjectDirectory)"
                    Items="@(MyItems)"
                    MetadataName="Value">
  <Output TaskParameter="ConvertedItems" ItemName="MyConvertedItems" />
</ConvertToWinePaths>

<!-- Copy the converted items back to MyItems, then empty MyConvertedItems -->
<ItemGroup Condition="$(UseWine)">
  <MyItems Remove="@(MyItems)" />
  <MyItems Include="@(MyConvertedItems)" />
  <MyConvertedItems Remove="@(MyConvertedItems)" />
</ItemGroup>
```

With `OnlyIfMetadata`, the task converts the items whose named metadata is `true`, ignoring case, and leaves the others as they are:

```xml
<ConvertToWinePaths Condition="$(UseWine)"
                    BasePath="$(MSBuildProjectDirectory)"
                    Items="@(MyItems)"
                    MetadataName="Value"
                    OnlyIfMetadata="IsPath">
  <Output TaskParameter="ConvertedItems" ItemName="MyConvertedItems" />
</ConvertToWinePaths>

<!-- Copy the converted items back to MyItems, then empty MyConvertedItems -->
<ItemGroup Condition="$(UseWine)">
  <MyItems Remove="@(MyItems)" />
  <MyItems Include="@(MyConvertedItems)" />
  <MyConvertedItems Remove="@(MyConvertedItems)" />
</ItemGroup>
```

### Invoking a tool through Wine

Take a Windows-only program, ExeMangler, that processes the executable file of an application.
Its NuGet package is referenced, the `ExeManglerFullPath` property holds its path, and it takes one argument: the full path of the executable to process.

On Windows alone, one target does the job:

```xml
<Target Name="InvokeExeMangler" AfterTargets="PostBuildEvent">

  <Exec Command="$(ExeManglerFullPath) $(TargetPath)" />

</Target>
```

The target below runs ExeMangler through Wine on Linux and macOS, and unchanged on Windows, where `UseWine` is `false`:

```xml
<!-- Activate the Wine module -->
<ItemGroup>
  <NeedWine Include="ExeMangler" />
</ItemGroup>

<Target Name="InvokeExeMangler" AfterTargets="PostBuildEvent">

  <!-- Two "local" properties for the paths to convert -->
  <PropertyGroup>
    <_TEMP_ExeManglerFullPath>$(ExeManglerFullPath)</_TEMP_ExeManglerFullPath>
    <_TEMP_TargetPath>$(TargetPath)</_TEMP_TargetPath>
  </PropertyGroup>

  <!-- Convert the paths when needed -->
  <GetWinePaths Condition="$(UseWine)"
                HostPath1="$(_TEMP_ExeManglerFullPath)"
                HostPath2="$(_TEMP_TargetPath)">
    <Output TaskParameter="WinePath1" PropertyName="_TEMP_ExeManglerFullPath" />
    <Output TaskParameter="WinePath2" PropertyName="_TEMP_TargetPath" />
  </GetWinePaths>

  <!-- Build the command line -->
  <PropertyGroup>
    <_TEMP_ExeManglerCommand>$(_TEMP_ExeManglerFullPath) $(_TEMP_TargetPath)</_TEMP_ExeManglerCommand>
  </PropertyGroup>

  <!-- Under Wine, prepend WineCommand to the command line -->
  <PropertyGroup Condition="$(UseWine)">
    <_TEMP_ExeManglerCommand>$(WineCommand) $(_TEMP_ExeManglerCommand)</_TEMP_ExeManglerCommand>
  </PropertyGroup>

  <!-- Invoke ExeMangler -->
  <Exec Command="$(_TEMP_ExeManglerCommand)" />

  <!-- Clear the "local" properties -->
  <PropertyGroup>
    <_TEMP_ExeManglerFullPath />
    <_TEMP_TargetPath />
    <_TEMP_ExeManglerCommand />
  </PropertyGroup>

</Target>
```

[`Module.Core.InnoSetup.targets`](../../src/Buildvana.Sdk/Modules/AlternatePack/Module.Core.InnoSetup.targets) shows how Buildvana SDK invokes the Inno Setup compiler the same way.

---

## Diagnostics

The module raises the diagnostics of the [Wine module (2200-2299)](../sdk-diagnostics.md#wine-module-2200-2299) range.
