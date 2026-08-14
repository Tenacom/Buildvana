// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

#:package Buildvana.Runtime

using System.IO;
using System.Text.RegularExpressions;
using Buildvana.Runtime;

// release/post-release hook: keeps the $schema URL in the configuration file pointing at the release
// tag of the version being released. The guard mirrors the built-in self-reference rewrites: the
// $schema URL is itself a self-reference, so it moves only when dogfooding moves the rest.
var hookArgs = PostReleaseHookArgs.Load();
if (!hookArgs.Dogfooding)
{
    return;
}

// AFTER THE NEXT RELEASE: replace the search below with
//     var configFile = hookArgs.RuntimeInfo.ConfigFile;
// keeping the null check. That member names the file bv itself read, which is what a hook rewriting the
// configuration file should act on, and what Hooks.md tells hooks to use instead of searching for one.
// It cannot be used yet: the SDK pins Buildvana.Runtime to its own version, so this hook compiles against
// the last published release, and RuntimeInfo.ConfigFile ships with the next one. Searching is correct in
// the meantime — it is the same search bv performs, over the directory bv reports as home — the hook just
// answers on its own rather than being told.
var configFile = BuildvanaConfig.FindFile(hookArgs.RuntimeInfo.HomeDirectory);
if (configFile is null)
{
    return;
}

// Same expression as SelfVersionService.SchemaUrlRegex in src/Buildvana.Tool, which `bv update` applies to
// consumer repositories' configuration files; keep the two copies identical.
var text = File.ReadAllText(configFile);
text = Regex.Replace(text, "(Tenacom/Buildvana/)[^/]+(/schemas/)", $"${{1}}{hookArgs.Release.SemVer}$2");
File.WriteAllText(configFile, text);
