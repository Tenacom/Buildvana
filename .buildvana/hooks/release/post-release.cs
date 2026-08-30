// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

#:package Buildvana.Runtime

using System.IO;
using System.Text.RegularExpressions;
using Buildvana.Runtime;

// release/post-release hook: promotes buildvana.next.jsonc over the configuration file, then keeps the
// file's $schema URL pointing at the release tag of the version being released. The guard mirrors the
// built-in self-reference rewrites: both edits follow the tool pin, so they move only when dogfooding
// moves the rest.
var hookArgs = PostReleaseHookArgs.Load();
if (!hookArgs.Dogfooding)
{
    return;
}

var configFile = hookArgs.RuntimeInfo.ConfigFile;
if (configFile is null)
{
    return;
}

// Promote the next configuration file before pinning the $schema URL below: the same commit moves the
// tool pin to the version being released, so the file and the tool that reads it arrive together. The
// next file sits beside the one it replaces, and is checked in, so a missing one is a broken tree:
// File.Copy throws, the hook exits non-zero, and the release fails naming the path. Skipping instead
// would revert the configuration file at the release after.
var nextConfigFile = Path.Combine(Path.GetDirectoryName(configFile)!, "buildvana.next.jsonc");
File.Copy(nextConfigFile, configFile, overwrite: true);

// Same expression as SelfVersionService.SchemaUrlRegex in src/Buildvana.Tool, which `bv self-update` applies to
// consumer repositories' configuration files; keep the two copies identical.
var text = File.ReadAllText(configFile);
text = Regex.Replace(text, "(Tenacom/Buildvana/)[^/]+(/schemas/)", $"${{1}}{hookArgs.Release.SemVer}$2");
File.WriteAllText(configFile, text);
