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

var configFile = hookArgs.RuntimeInfo.ConfigFile;
if (configFile is null)
{
    return;
}

// Same expression as SelfVersionService.SchemaUrlRegex in src/Buildvana.Tool, which `bv update` applies to
// consumer repositories' configuration files; keep the two copies identical.
var text = File.ReadAllText(configFile);
text = Regex.Replace(text, "(Tenacom/Buildvana/)[^/]+(/schemas/)", $"${{1}}{hookArgs.Release.SemVer}$2");
File.WriteAllText(configFile, text);
