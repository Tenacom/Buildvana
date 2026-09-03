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
    private const string DefaultTargetFramework = "net10.0";

    private readonly List<(string TargetFramework, string Id, string Version)> _packages = [];
    private readonly List<(string TargetFramework, string Id)> _direct = [];
    private readonly List<string> _frameworks = [];
    private readonly List<string> _logs = [];

    private bool _pinsTransitively;

    /// <summary>
    /// States a package the restore resolved.
    /// </summary>
    /// <param name="id">The package id.</param>
    /// <param name="version">The version resolved.</param>
    /// <param name="direct">Whether the project references the package itself.</param>
    /// <param name="targetFramework">The target framework whose graph resolved it.</param>
    /// <returns>This instance, for chaining.</returns>
    public AssetsFile Resolves(
        string id,
        string version,
        bool direct = false,
        string targetFramework = DefaultTargetFramework)
    {
        Include(targetFramework);
        _packages.Add((targetFramework, id, version));
        if (direct)
        {
            _direct.Add((targetFramework, id));
        }

        return this;
    }

    /// <summary>
    /// States that the project raises the version of its transitive dependencies from its central pins, which
    /// is <c>CentralPackageTransitivePinningEnabled</c> together with central package management itself.
    /// </summary>
    /// <returns>This instance, for chaining.</returns>
    public AssetsFile PinsTransitively()
    {
        _pinsTransitively = true;
        return this;
    }

    /// <summary>
    /// States an entry of the restore's log.
    /// </summary>
    /// <param name="code">The NuGet code.</param>
    /// <param name="libraryId">The package the entry is about, empty where it is about none.</param>
    /// <param name="level">The level, as the file spells it.</param>
    /// <param name="targetFramework">The target framework the entry is about.</param>
    /// <returns>This instance, for chaining.</returns>
    public AssetsFile Reports(
        string code,
        string libraryId = "",
        string level = "Warning",
        string targetFramework = DefaultTargetFramework)
    {
        Include(targetFramework);
        var message = $"{code} about {(libraryId.Length == 0 ? "the restore" : libraryId)}";
        _logs.Add($$"""
                        {
                          "code": "{{code}}",
                          "level": "{{level}}",
                          "warningLevel": 1,
                          "message": "{{message}}",
                          "libraryId": "{{libraryId}}",
                          "targetGraphs": [ "{{targetFramework}}" ]
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
        List<string> frameworks = _frameworks.Count > 0 ? _frameworks : [DefaultTargetFramework];
        var targets = string.Join(",\n", frameworks.Select(TargetOf));
        var projectFrameworks = string.Join(",\n", frameworks.Select(FrameworkOf));
        var restoreFrameworks = string.Join(
            ",\n",
            frameworks.Select(static framework => $$"""        "{{framework}}": { "targetAlias": "{{framework}}" }"""));

        var originalFrameworks = string.Join(", ", frameworks.Select(static framework => $"\"{framework}\""));
        var dependencyGroups = string.Join(", ", frameworks.Select(static framework => $"\"{framework}\": []"));
        var centralPinning = _pinsTransitively ? "true" : "false";
        var logs = string.Join(",\n", _logs);
        return $$"""
                            {
                              "version": 4,
                              "targets": {
                            {{targets}}
                              },
                              "libraries": {},
                              "projectFileDependencyGroups": { {{dependencyGroups}} },
                              "packageFolders": {},
                              "project": {
                                "version": "1.0.0",
                                "restore": {
                                  "projectUniqueName": "Test.csproj",
                                  "projectName": "Test",
                                  "projectPath": "Test.csproj",
                                  "projectStyle": "PackageReference",
                                  "centralPackageVersionsManagementEnabled": {{centralPinning}},
                                  "CentralPackageTransitivePinningEnabled": {{centralPinning}},
                                  "originalTargetFrameworks": [ {{originalFrameworks}} ],
                                  "frameworks": {
                            {{restoreFrameworks}}
                                  }
                                },
                                "frameworks": {
                            {{projectFrameworks}}
                                }
                              },
                              "logs": [
                            {{logs}}
                              ]
                            }
                            """;
    }

    // The frameworks the file states, in the order the fixture met them.
    private void Include(string targetFramework)
    {
        if (!_frameworks.Contains(targetFramework))
        {
            _frameworks.Add(targetFramework);
        }
    }

    // One target graph, holding the packages that framework resolved.
    private string TargetOf(string framework)
    {
        var packages = string.Join(
            ",\n",
            _packages
                .Where(package => package.TargetFramework == framework)
                .Select(static package => $$"""      "{{package.Id}}/{{package.Version}}": { "type": "package" }"""));

        return $$"""
                    "{{framework}}": {
                {{packages}}
                    }
                """;
    }

    // What the project states for one framework: its alias, and the packages it references itself.
    private string FrameworkOf(string framework)
    {
        var dependencies = string.Join(
            ",\n",
            _direct
                .Where(reference => reference.TargetFramework == framework)
                .Select(static reference => $$"""          "{{reference.Id}}": { "target": "Package", "version": "[1.0.0, )" }"""));

        return $$"""
                      "{{framework}}": {
                        "targetAlias": "{{framework}}",
                        "dependencies": {
                {{dependencies}}
                        }
                      }
                """;
    }
}
