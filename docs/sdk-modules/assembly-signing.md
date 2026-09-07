# `AssemblySigning` module

This module signs an assembly with a strong name from a `.pfx` certificate file.
Under `dotnet build`, the .NET SDK refuses a `.pfx` file as the strong-name key file, with `PFX signing not supported on .NET Core`.
The module extracts the RSA private key of the certificate into a temporary `.snk` file, and the compiler signs with that file.

---

<!-- markdownlint-disable MD036 -->
**Table of contents**
<!-- markdownlint-enable MD036 -->

- [Configuration](#configuration)
  - [`AssemblyOriginatorKeyFile` property](#assemblyoriginatorkeyfile-property)
  - [`AssemblyOriginatorKeyPassword` property](#assemblyoriginatorkeypassword-property)
- [Usage](#usage)
  - [Temporary key file](#temporary-key-file)
- [Diagnostics](#diagnostics)

---

## Configuration

The module has no property of its own.
It acts when `SignAssembly` is `true` and `AssemblyOriginatorKeyFile` names a `.pfx` file.
`AssemblyOriginatorKeyFile` is a [common MSBuild property](https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-properties), and `SignAssembly` turns strong-name signing on.
The module does nothing when `SignAssembly` is not `true`, or when the key file is an `.snk` file, which the .NET SDK reads itself.

### `AssemblyOriginatorKeyFile` property

The path of the key file.
The extension comparison ignores case, so `.PFX` names a certificate file too.
A path that names no file fails the build with BVSDK1200.

Set the property in `Common.props`, so that every project of the repository signs with the same certificate:

```xml
<PropertyGroup>
  <SignAssembly>true</SignAssembly>
  <AssemblyOriginatorKeyFile>$(MSBuildThisFileDirectory)signing.pfx</AssemblyOriginatorKeyFile>
</PropertyGroup>
```

### `AssemblyOriginatorKeyPassword` property

The password of the `.pfx` file.
The default is empty, which means that the file has no password.
A wrong password, or a missing one, fails the build with BVSDK1201.

Do not write the password in a file of the repository.
MSBuild reads every environment variable as a property, so an environment variable named `AssemblyOriginatorKeyPassword` sets it.
A CI secret reaches the build the same way.

---

## Usage

### Temporary key file

`ResolveKeySource` is the .NET SDK target that reads `AssemblyOriginatorKeyFile`.
Before it runs, the module runs the `ConvertPfxToSnk` task.
The task loads the certificate, exports its RSA private key, and writes the key to `BuildvanaTemp.snk` in the intermediate output directory of the project.
That directory is `obj/<Configuration>/<TargetFramework>/` by default.
The module then points `AssemblyOriginatorKeyFile` at that file, and the compiler signs the assembly with it.
The `Clean` target deletes the file.

The certificate must hold an RSA private key, because a strong name is an RSA signature.
A certificate with a key of another type, such as ECDSA, fails the build with BVSDK1202.

> [!WARNING]
> The temporary file holds the private key unencrypted.
> Keep the `obj` directory out of the repository, as the `.gitignore` file that `dotnet new gitignore` writes does.

---

## Diagnostics

The module raises the diagnostics of the [AssemblySigning module (1200-1299)](../sdk-diagnostics.md#assemblysigning-module-1200-1299) range.
