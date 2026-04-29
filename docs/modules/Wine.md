# `Wine` module

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Overview](#overview)
- [Configuration](#configuration)
  - [`WineCommand` property](#winecommand-property)
- [Usage](#usage)
  - [`NeedWine` items](#needwine-items)
  - [`UseWine` property](#usewine-property)
  - [Convert all paths](#convert-all-paths)
    - [`GetWinePath` task](#getwinepath-task)
    - [`GetWinePaths` task](#getwinepaths-task)
    - [`ConvertToWinePaths` task](#converttowinepaths-task)
  - [Putting it all together: invoking a tool through Wine](#putting-it-all-together-invoking-a-tool-through-wine)

## Overview

This module provides support for running Windows-only tools using [Wine](https://winehq.org) when building under Linux or macOS.

## Configuration

The following property can be defined in a [configuration file](../ConfigurationFiles.md).

### `WineCommand` property

Wine can be configured and fine-tuned in lots of ways; you may want to run your build tools in a different configuration from games or productivity applications.

A simple command line like `wine SomeTool.exe param1 param2` will almost never suffice: some environment variable such as `WINEPREFIX` might have to be set, some registry tweak might be needed, and so on.

Buildvana SDK requires that a `WineCommand` property be set to the command you want to use to run tools in Wine, for example:

```xml
  <PropertyGroup>
    <WineCommand>WINEPREFIX=~/.alternateWineConfiguration wine</WineCommand>
  </PropertyGroup>
```

or the path to a script to the same effect, for example:

```xml
  <PropertyGroup>
    <WineCommand>/usr/local/bin/run-wine</WineCommand>
  </PropertyGroup>
```

For an example of script used to run Wine. you can take a look at [`buildvana-builder`](https://github.com/Tenacom/buildvana-builder), a Docker image based on Ubuntu LTS, featuring the .NET SDK and [Inno Setup](https://jrsoftware.org/isinfo.php)'s command-line compiler.

## Usage

### `NeedWine` items

If you use a Windows-only tool in your build process, and you want to run it with Wine when building on Linux or macOS, you must define a `NeedWine` item with the name of the tool, like this:

```xml
  <ItemGroup>
    <NeedWine Include="ToolName" />
  </ItemGroup>
```

`NeedWine` items MUST be defined outside any target. The name you use is just used for informative purposes; it needs not be the name of the executable.

If at least one `NeedWine` item is defined, and the `WineCommand` property is not set (or is set to an empty string) after MSBuild's evaluation phase, Buildvana SDK will issue [error BVSDK2200](../Diagnostics.md#wine-module-2200-2299) with a message listing the names of the tools that need Wine.

### `UseWine` property

Buildvana SDK will set the `UseWine` property to `true` if at least one `NeedWine` item is defined, _and_ the `WineCommand` property is set to a non-empty string. It will do so in a target run very early during the build process.

When building on Windows, or if no `NeedWine` item is defined, `UseWine` will always be set to `false`.

During MSBuild's evaluation phase, `UseWine` will still be empty.

### Convert all paths

Practically any tool needs to be passed paths to input files, output files, configuration files, you name it. You can usually count on MSBuild's [property functions](https://learn.microsoft.com/en-us/visualstudio/msbuild/property-functions) to construct and combine such paths; not as long as Wine is part of the equation, though.

When a tool runs with Wine, it "thinks" it is running on Windows, and of course need Windows-style paths, with drive letters and backspaces as separators. Wine itself needs the path to the tool's executable to be a Windows path. MSBuild, on the other hand, is running under an Unix-like operating system and behaves accordingly: it will [accept backslashes as path separators](https://github.com/dotnet/msbuild/issues/1024), as a tribute to its Windows / .NET Framework roots, but it knows nothing about drive letters, for example.

Converting a Unix-style path to a Windows path usable by Wine is not complicated and could even be done in MSBuild. By default, Wine maps the  `Z:` drive to the root filesystem, so that a _full_ path is easily transformed from, for example, `/usr/share/some/path` to `Z:\usr\share\some\path`.

```xml
  <PropertyGroup>
    <MyFullPath>Z:$(MyFullPath.Replace('/', '\\'))</MyFullPath>
  </PropertyGroup>
```

What if you have a relative path, for example relative to the project directory, as is pretty customary in MSBuild? Just turn it to a full path before conversion:

```xml
  <PropertyGroup>
    <MyPath>$([System.IO.Path]::Combine('$(MSBuildProjectDirectory)', '$(MyPath)'))</MyPath>
    <MyPath>Z:$(MyPath.Replace('/', '\\'))</MyPath>
  </PropertyGroup>
```

OK, this is getting pretty ugly pretty fast. Isn't there a cleaner, less verbose, less error-prone way to convert Unix-like paths to Wine-digestable Windows-style paths?

Thanks to Buildvana SDK's compiled tasks, the answer is yes... although, this being MSBuild, we couldn't do much about verbosity.

#### `GetWinePath` task

To convert a Unix-style full path to a Wine path, you can use the `GetWinePath` task:

```xml
  <GetWinePath Condition="$(UseWine)"
               HostPath="$(MyPath)">
    <Output TaskParameter="WinePath" PropertyName="MyPath" />
  </GetWinePath>
```

For a relative path, just use the optional `BasePath` parameter:

```xml
  <GetWinePath Condition="$(UseWine)"
               BasePath="$(MSBuildProjectDirectory)"
               HostPath="$(MyPath)">
    <Output TaskParameter="WinePath" PropertyName="MyPath" />
  </GetWinePath>
```

The `Condition="$(UseWine)"` attribute ensures that the task will _not_ be used when building on Windows.

#### `GetWinePaths` task

To convert more than one path (up to 10) you can use the `GetWinePaths` task:

```xml
  <GetWinePaths Condition="$(UseWine)"
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
  </GetWinePath>
```

The optional `BasePath` parameter will be valid applied to all paths:

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

What if the paths to convert are stored in an item group? Just use the `ConvertToWinePaths` task.

Note that in this case you can't use the same item group to store the results directly, as MSBuild will just append them to existing items.

```xml
  <!-- BasePath is optional, as usual -->
  <ConvertToWinePaths Condition="$(UseWine)"
                      BasePath="$(MSBuildProjectDirectory)"
                      Items="@(MyPaths)">
    <Output TaskParameter="ComvertedItems" ItemName="MyConvertedPaths" />
  </ConvertToWinePaths>

  <!-- Copy converted paths back to MyPaths, then free up memory by emptying MyConvertedPaths -->
  <ItemGroup Condition="$(UseWine)">
    <MyPaths Remove="@(MyPaths)" />
    <MyPaths Include="@(MyConvertedPaths)" />
    <MyConvertedPaths Remove="@(MyConvertedPaths)" />
  </ItemGroup>
```

`ConvertToWinePaths` can do more than that. Say, for example, that the paths you want to convert are not the items' identities, but rather in a `Value` metadata:

```xml
  <!-- BasePath is optional, as usual -->
  <ConvertToWinePaths Condition="$(UseWine)"
                      BasePath="$(MSBuildProjectDirectory)"
                      Items="@(MyItems)"
                      MetadataName="Value">
    <Output TaskParameter="ComvertedItems" ItemName="MyConvertedItems" />
  </ConvertToWinePaths>

  <!-- Copy converted items back to MyItems, then free up memory by emptying MyConvertedItems -->
  <ItemGroup Condition="$(UseWine)">
    <MyItems Remove="@(MyItems)" />
    <MyItems Include="@(MyConvertedItems)" />
    <MyConvertedItems Remove="@(MyConvertedItems)" />
  </ItemGroup>
```

Finally, if not _all_ `Value` metadata values are paths to convert, you can use another metadata as a flag to signal which items to convert.

The following code will only convert the `Value` metadata of items whose `IsPath` metadata evaluates (case-insensitively) to `"true"`, leaving other items unchanged:

```xml
  <!-- BasePath is optional, as usual -->
  <ConvertToWinePaths Condition="$(UseWine)"
                      BasePath="$(MSBuildProjectDirectory)"
                      Items="@(MyItems)"
                      MetadataName="Value"
                      OnlyIfMetadata="IsPath">
    <Output TaskParameter="ComvertedItems" ItemName="MyConvertedItems" />
  </ConvertToWinePaths>

  <!-- Copy converted items back to MyItems, then free up memory by emptying MyConvertedItems -->
  <ItemGroup Condition="$(UseWine)">
    <MyItems Remove="@(MyItems)" />
    <MyItems Include="@(MyConvertedItems)" />
    <MyConvertedItems Remove="@(MyConvertedItems)" />
  </ItemGroup>
```

### Putting it all together: invoking a tool through Wine

Let's pretend there's a Windows-only program, called _ExeMangler_, that takes our compiled application's EXE file and does something with it. What it does is not important, as this is only an example.

_ExeMangler_ comes in its own NuGet package, which of course we referenced, and the full path to it is in the `ExeManglerFullPath` property. _ExeMangler_ must be invoked with exactly one parameter: the full path to the executable file to work on.

If we only built our application on Windows, the code would be pretty straightforward:

```xml
<Target Name="InvokeExeMangler" AfterTargets="PostBuildEvent">

  <Exec Command="$(ExeManglerFullPath) $(TargetPath)" />

</Target>
```

Here's how we can support running _ExeMangler_ through Wine when building on Linux or macOS. Note that nothing changes when the `UseWine` property is `false`, i.e. on Windows:

```xml
<!-- Trigger Wine module -->
<ItemGroup>
  <NeedWine Include="ExeMangler" />
</ItemGroup>

<Target Name="InvokeExeMangler" AfterTargets="PostBuildEvent">

  <!-- Let's use two "local" properties for the paths we need to convert -->
  <PropertyGroup>
    <_TEMP_ExeManglerFullPath>$(ExeManglerFullPath)</_TEMP_ExeManglerFullPath>
    <_TEMP_TargetPath>$(TargetPath)</_TEMP_TargetPath>
  </PropertyGroup>

  <!-- Convert paths when needed -->
  <GetWinePaths Condition="$(UseWine)"
                HostPath1="$(_TEMP_ExeManglerFullPath)"
                HostPath2="$(_TEMP_TargetFullPath)">
    <Output TaskParameter="WinePath1" PropertyName="_TEMP_ExeManglerFullPath" />
    <Output TaskParameter="WinePath2" PropertyName="_TEMP_TargetFullPath" />
  </GetWinePaths>

  <!-- Construct the command line for ExeMangler -->
  <PropertyGroup>
    <_TEMP_ExeManglerCommand>$(_TEMP_ExeManglerFullPath) $(_TEMP_TargetFullPath)</_TEMP_ExeManglerCommand>
  </PropertyGroup>

  <!-- When using Wine, prepend WineCommand to the command line -->
  <PropertyGroup Condition="$(UseWine)">
    <_TEMP_ExeManglerCommand>$(WineCommand) $(_TEMP_ExeManglerCommand)</_TEMP_ExeManglerCommand>
  </PropertyGroup>

  <!-- Invoke ExeMangler -->
  <Exec Command="$(_TEMP_ExeManglerCommand)" />

  <!-- Clear "local" properties -->
  <PropertyGroup>
    <_TEMP_ExeManglerFullPath />
    <_TEMP_TargetPath />
    <_TEMP_ExeManglerCommand />
  </PropertyGroup>

</Target>
```

For a more convoluted example, you can take a look at [how Buildvana SDK invokes Inno Setup's compiler](https://github.com/Tenacom/Buildvana/blob/main/src/Buildvana.Sdk/Modules/AlternatePack/Module.Core.InnoSetup.targets).
