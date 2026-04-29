# Buildvana SDK configuration files

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Buildvana SDK configuration files](#buildvana-sdk-configuration-files)
  - [Overview](#overview)
    - [What are configuration files?](#what-are-configuration-files)
    - [Why configuration files?](#why-configuration-files)
    - [Configuration file locations](#configuration-file-locations)
    - [A word to the wise](#a-word-to-the-wise)
  - [Configuration files, one by one](#configuration-files-one-by-one)
    - [`Buildvana.Sdk.props`](#buildvanasdkprops)

## Overview

### What are configuration files?

Buildvana SDK configuration files are `.props` and/or `.targets` files whose scope is not a single project or a repository, but a machine or a user.

### Why configuration files?

At [Tenacom](https://github.com/Tenacom) we strive to minimize the amount of preliminary setup a developer needs to work on a project. Ideally, one should be able to clone a repository, open an IDE (either Visual Studio, Rider, or VS Code) and start working, no additional installations required.

That having been said, there are situations where different actions are required to achieve the same results on different machines (say, CI/CD builders vs. developer workstations) and/or for different users. A notable example is invoking [Wine](https://winehq.org) to run a Windows-only tool in the context of a build on a Linux or Mac system (see [Wine module's documentation](modules/Wine.md) for details).

Configuration files are the exception to the basic rule that every file needed for the build must either reside in the repository, or in a NuGet package.

### Configuration file locations

Buildvana SDK looks for configuration files in the following directories, according to the operating system:

| Windows                                      | macOS                              | Linux                             |
| -------------------------------------------- | ---------------------------------- | --------------------------------- |
|                                              | /etc/buildvana                     | /etc/buildvana                    |
| C:\\ProgramData\\buildvana                   | /usr/share/buildvana               | /usr/share/buildvana              |
| C:\\Users\\John\\.buildvana                  | /Users/john/.buildvana             | /home/john/.buildvana             |
| C:\\Users\\John\\AppData\\Roaming\\buildvana | /Users/john/.config/buildvana      | /home/john/.config/buildvana      |
| C:\\Users\\John\\AppData\\Local\\buildvana   | /Users/john/.local/share/buildvana | /home/john/.local/share/buildvana |

_(Note that the above table assumes default folder locations and a user name of `John` (on Windows) or `john` (on other systems). If, for example, the user profile is in `D:\Some\Path\Users\Paul`, Buildvana SDK will look for configuration files in `D:\Some\Path\Users\Paul\.buildvana`, etc.)_

If a configuration file with the same name is present in more than one of the above locations, Buildvana SDK will import all of them, in the order specified by the table above.

### A word to the wise

Since configuration files are just XML files imported into every project, the possibilities they open up are endless... _in theory_.

In practice, though, you should keep their contents to a minimum and only use them for the purposes specified in this documentation (particularly in the "Configuration" section of module docs, when present).

Remember that configuration files have the potential to introduce a "surprise factor" in your builds. MSBuild _hates_ surprises and is a vindictive b*tch. You have been warned.

## Configuration files, one by one

### `Buildvana.Sdk.props`

This file is imported, if present, before any module and `Common.props` file. Its purpose is to set global properties that can vary by machine and/or user.

An example of `Buildvana.Sdk.props`:

```xml
<Project>

  <!-- Wine module configuration -->
  <PropertyGroup>
    <WineCommand>/usr/local/bin/wine-run</WineCommand>
  <PropertyGroup>

</Project>
```
