// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

// release/post-release hook: keeps the $schema URL in buildvana.jsonc pointing at the release tag
// of the version being released. The guard mirrors the built-in self-reference rewrites: the
// $schema URL is itself a self-reference, so it moves only when dogfooding moves the rest.
//
// Bootstrap note: this repository builds hooks with the previously published Buildvana SDK, which
// does not ship BvHookContext yet — the context file is read directly from its well-known path.
// Consumer hooks can use `var context = BvHookContext.Load();` instead.
using var context = JsonDocument.Parse(File.ReadAllText(".buildvana-temp/hook-context.json"));
if (!context.RootElement.GetProperty("dogfooded").GetBoolean())
{
    return;
}

var version = context.RootElement.GetProperty("releaseSemVer").GetString()!;
var path = "buildvana.jsonc";
var text = File.ReadAllText(path);
text = Regex.Replace(text, "(Tenacom/Buildvana/)[^/]+(/schemas/)", $"${{1}}{version}$2");
File.WriteAllText(path, text);
