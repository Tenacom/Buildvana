# Buildvana SDK configuration files

A Buildvana SDK configuration file is a `.props` file whose scope is a machine or a user, not a repository.
This page says what the files are for, where Buildvana SDK looks for them, and what each one holds.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Purpose](#purpose)
- [Locations](#locations)
- [`Buildvana.Sdk.props`](#buildvanasdkprops)

---

## Purpose

Every file a build needs resides in the repository or in a NuGet package, so that a clone builds the same way on every machine.
A configuration file is the exception.
It holds a setting that differs between machines or between users, and that no repository can state.
The `WineCommand` property of the [`Wine` module](sdk-modules/wine.md) is one.
A CI image runs Windows-only tools through a script of its own, and a developer workstation through another.

Keep a configuration file to the settings that the module pages name under "Configuration".
Any other property it sets reaches every project of every repository on the machine, and makes a build hard to reproduce.

---

## Locations

Buildvana SDK imports `Buildvana.Sdk.props` from each directory below that holds one, in the order of the table.
A later file overrides a property an earlier one sets.

| Windows                                     | macOS                                | Linux                               |
| ------------------------------------------- | ------------------------------------ | ----------------------------------- |
|                                             | `/etc/buildvana`                     | `/etc/buildvana`                    |
| `C:\ProgramData\buildvana`                  | `/usr/share/buildvana`               | `/usr/share/buildvana`              |
| `C:\Users\John\.buildvana`                  | `/Users/john/.buildvana`             | `/home/john/.buildvana`             |
| `C:\Users\John\AppData\Roaming\buildvana`   | `/Users/john/.config/buildvana`      | `/home/john/.config/buildvana`      |
| `C:\Users\John\AppData\Local\buildvana`     | `/Users/john/.local/share/buildvana` | `/home/john/.local/share/buildvana` |

The table assumes the default folder locations and a user named `John` on Windows, or `john` elsewhere.
Buildvana SDK asks the operating system for the folder locations, so a user profile at `D:\Some\Path\Users\Paul` yields `D:\Some\Path\Users\Paul\.buildvana`.

---

## `Buildvana.Sdk.props`

`Sdk.props` imports the file before every module and before `Common.props`.
Its purpose is to set the properties that vary by machine or by user.

```xml
<Project>

  <!-- Wine module configuration -->
  <PropertyGroup>
    <WineCommand>/usr/local/bin/wine-run</WineCommand>
  </PropertyGroup>

</Project>
```
