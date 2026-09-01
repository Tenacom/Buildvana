// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

/// <summary>
/// Builds the content of a <c>project.assets.json</c> for one target framework: what a restore would have
/// written, cut down to the sections <c>bv</c> reads.
/// </summary>
/// <remarks>
/// <para>The restore metadata's framework aliases are not decoration: NuGet's reader resolves a framework of
/// the project through its alias, and a file without them fails to parse as a whole.</para>
/// </remarks>
internal sealed class AssetsFile
{
    private const string TargetFramework = "net10.0";

    private readonly List<(string Id, string Version)> _packages = [];
    private readonly List<string> _direct = [];
    private readonly List<string> _logs = [];

    /// <summary>
    /// States a package the restore resolved.
    /// </summary>
    /// <param name="id">The package id.</param>
    /// <param name="version">The version resolved.</param>
    /// <param name="direct">Whether the project references the package itself.</param>
    /// <returns>This instance, for chaining.</returns>
    public AssetsFile Resolves(string id, string version, bool direct = false)
    {
        _packages.Add((id, version));
        if (direct)
        {
            _direct.Add(id);
        }

        return this;
    }

    /// <summary>
    /// States an entry of the restore's log.
    /// </summary>
    /// <param name="code">The NuGet code.</param>
    /// <param name="libraryId">The package the entry is about, empty where it is about none.</param>
    /// <param name="level">The level, as the file spells it.</param>
    /// <returns>This instance, for chaining.</returns>
    public AssetsFile Reports(string code, string libraryId = "", string level = "Warning")
    {
        var message = $"{code} about {(libraryId.Length == 0 ? "the restore" : libraryId)}";
        _logs.Add($$"""
                        {
                          "code": "{{code}}",
                          "level": "{{level}}",
                          "warningLevel": 1,
                          "message": "{{message}}",
                          "libraryId": "{{libraryId}}",
                          "targetGraphs": [ "{{TargetFramework}}" ]
                        }
                    """);

        return this;
    }

    /// <summary>
    /// Builds the file's content.
    /// </summary>
    /// <returns>The JSON a restore would have written.</returns>
    public override string ToString()
    {
        var targets = string.Join(
            ",\n",
            _packages.Select(static package => $$"""      "{{package.Id}}/{{package.Version}}": { "type": "package" }"""));

        var dependencies = string.Join(
            ",\n",
            _direct.Select(static id => $$"""          "{{id}}": { "target": "Package", "version": "[1.0.0, )" }"""));

        var logs = string.Join(",\n", _logs);
        return $$"""
                            {
                              "version": 4,
                              "targets": {
                                "{{TargetFramework}}": {
                            {{targets}}
                                }
                              },
                              "libraries": {},
                              "projectFileDependencyGroups": { "{{TargetFramework}}": [] },
                              "packageFolders": {},
                              "project": {
                                "version": "1.0.0",
                                "restore": {
                                  "projectUniqueName": "Test.csproj",
                                  "projectName": "Test",
                                  "projectPath": "Test.csproj",
                                  "projectStyle": "PackageReference",
                                  "originalTargetFrameworks": [ "{{TargetFramework}}" ],
                                  "frameworks": {
                                    "{{TargetFramework}}": { "targetAlias": "{{TargetFramework}}" }
                                  }
                                },
                                "frameworks": {
                                  "{{TargetFramework}}": {
                                    "targetAlias": "{{TargetFramework}}",
                                    "dependencies": {
                            {{dependencies}}
                                    }
                                  }
                                }
                              },
                              "logs": [
                            {{logs}}
                              ]
                            }
                            """;
    }
}
