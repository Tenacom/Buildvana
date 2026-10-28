// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Buildvana.Core.Testing;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;

// Evaluates the real Dependencies module props against a temporary repository, to see which transitive
// override files it imports. Evaluation only: importing a file is an evaluation-time decision, and what the
// imported files declare are items, which a project carries without building anything.
internal sealed class TransitiveOverridesFixture : IDisposable
{
    private readonly string _root;

    public TransitiveOverridesFixture()
    {
        _root = Path.Combine(Path.GetTempPath(), "bvtest_" + Guid.NewGuid().ToString("N"));
        ProjectDirectory = Path.Combine(_root, "src", "Test");
        _ = Directory.CreateDirectory(ProjectDirectory);
    }

    public string ProjectDirectory { get; }

    public void WriteHomeFile(string fileName, string content) => Write(Path.Combine(_root, fileName), content);

    public void WriteProjectFile(string fileName, string content)
        => Write(Path.Combine(ProjectDirectory, fileName), content);

    // The identities of the items of one type the evaluation ends up with, in evaluation order.
    public IReadOnlyList<string> EvaluateItems(string itemType, bool suppressOverrides = false)
    {
        var projectPath = Path.Combine(ProjectDirectory, "Test.proj");
        var content = $"""
                       <Project>
                         <PropertyGroup>
                           <HomeDirectory>{_root}{Path.DirectorySeparatorChar}</HomeDirectory>
                         </PropertyGroup>
                         <Import Project="{GetMetadata("RealDependenciesModulePropsPath")}" />
                       </Project>
                       """;
        Write(projectPath, content);
        var globalProperties = new Dictionary<string, string>();
        if (suppressOverrides)
        {
            globalProperties["BV_SuppressTransitiveOverrides"] = "true";
        }

        using var collection = new ProjectCollection(globalProperties);
        var project = Project.FromFile(projectPath, new ProjectOptions { ProjectCollection = collection });
        return [.. project.GetItems(itemType).Select(static item => item.EvaluatedInclude)];
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string GetMetadata(string key)
        => typeof(TransitiveOverridesFixture).Assembly.GetMetadata(key);

    private static void Write(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        Debug.Assert(directory is not null, "GetDirectoryName returns null only for a root, and a file path is not one.");
        _ = Directory.CreateDirectory(directory);
        File.WriteAllText(path, content);
    }
}
