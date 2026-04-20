// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Tool.Infrastructure;

public sealed class EnvVar
{
    public static readonly EnvVar GitHubToken = new("GITHUB_TOKEN");
    public static readonly EnvVar NuGetToken = new("NUGET_TOKEN");

    private EnvVar(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public string? GetValue() => Environment.GetEnvironmentVariable(Name);

    public void AssertHasValue()
    {
        if (string.IsNullOrEmpty(GetValue()))
        {
            throw new InvalidOperationException($"Environment variable '{Name}' is not specified!");
        }
    }

    public void SetEmpty() => Environment.SetEnvironmentVariable(Name, string.Empty);
}
